using System.Globalization;
using System.Text.RegularExpressions;
using ShipLean.Application.Plans;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.TaskChanges;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Planning.Reports;

// What report_task checks before it writes anything, so a call that is refused leaves nothing behind.
internal static partial class ReportChecks
{
    // The address rule link cards apply: trimmed, at most 2048 characters, absolute http or https.
    private const int MaxUrlLength = 2048;

    // A commit subject becomes a link title and a left-undone item a task title. Link sources cap their own titles at 255, and work items have no cap of their own.
    private const int MaxTitleLength = 255;

    public static ReportStatus ParseStatus(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "done" => ReportStatus.Done,
            "blocked" => ReportStatus.Blocked,
            "partial" => ReportStatus.Partial,
            _ => throw ApiException.Validation($"\"{status}\" is not a status. Use done, blocked or partial.", "status"),
        };

    // The left-undone items, trimmed and de-duplicated; each becomes an orphan task, so the count and the length are bounded.
    public static IReadOnlyList<string> LeftUndone(IReadOnlyList<string>? items)
    {
        CheckCount(items?.Count ?? 0, "left_undone");
        var kept = new List<string>();
        for (var i = 0; i < (items?.Count ?? 0); i++)
        {
            if (items![i] is null)
            {
                throw ApiException.Validation($"left_undone[{i}] is empty.", $"left_undone[{i}]");
            }
            var item = items[i].Trim();
            if (item.Any(char.IsControl))
            {
                // Each item becomes an orphan task's title, which a later brief puts in its heading: one line only.
                throw ApiException.Validation($"left_undone[{i}] is one line: no line breaks, tabs or other control characters.", $"left_undone[{i}]");
            }
            if (item.Length > MaxTitleLength)
            {
                throw ApiException.Validation($"left_undone[{i}] is {item.Length} characters; keep each item to {MaxTitleLength}.", $"left_undone[{i}]");
            }
            if (item.Length > 0 && !kept.Contains(item))
            {
                kept.Add(item);
            }
        }
        return kept;
    }

    // A null entry in a list of lines is the caller's mistake, not a server error.
    public static void CheckLists(IReadOnlyList<string>? testsRun, IReadOnlyList<string>? acceptanceTests)
    {
        CheckNoNulls(testsRun, "tests_run");
        CheckNoNulls(acceptanceTests, "acceptance_tests");
    }

    public static void CheckLinks(IReadOnlyList<ReportCommit> commits, string? prUrl)
    {
        CheckCount(commits.Count, "commits");
        for (var i = 0; i < commits.Count; i++)
        {
            if (commits[i] is null)
            {
                throw ApiException.Validation($"commits[{i}] is empty.", $"commits[{i}]");
            }
            if (commits[i].Subject?.Trim() is { Length: > MaxTitleLength } subject)
            {
                throw ApiException.Validation($"commits[{i}].subject is {subject.Length} characters; keep it to {MaxTitleLength}.", $"commits[{i}].subject");
            }
            if (!PlanMarkers.IsCommitSha(commits[i].Sha?.Trim()))
            {
                throw ApiException.Validation($"commits[{i}] needs a sha of at least 7 hex characters.", $"commits[{i}].sha");
            }
            var hasRepo = !string.IsNullOrWhiteSpace(commits[i].Repo);
            var hasUrl = !string.IsNullOrWhiteSpace(commits[i].Url);
            if ((!hasRepo || hasUrl) && !IsWebAddress(commits[i].Url))
            {
                throw ApiException.Validation($"commits[{i}] needs a repo, or a url that starts with http:// or https://.", $"commits[{i}].url");
            }
        }

        if (prUrl is not null && !IsWebAddress(prUrl))
        {
            throw ApiException.Validation("pr_url must start with http:// or https://.", "pr_url");
        }
    }

    // Each commit's committed_at as UTC (null when not sent). It needs a date, a time and an offset (Z or +hh:mm), and may be at most a day ahead of now.
    public static IReadOnlyList<DateTimeOffset?> CommitTimes(IReadOnlyList<ReportCommit> commits, DateTimeOffset now)
    {
        var times = new List<DateTimeOffset?>();
        for (var i = 0; i < commits.Count; i++)
        {
            var field = $"commits[{i}].committed_at";
            var text = commits[i].CommittedAt?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                times.Add(null);
                continue;
            }
            if (!WithOffset().IsMatch(text) || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                throw ApiException.Validation($"{field} must be ISO 8601 with an offset, such as 2026-11-16T09:30:00+02:00 or 2026-11-16T07:30:00Z; \"{text}\" is not.", field);
            }
            if (parsed > now.AddDays(1))
            {
                throw ApiException.Validation($"{field} is later than a day from now ({parsed:O}).", field);
            }
            times.Add(parsed.ToUniversalTime());
        }
        return times;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:?\d{2})$")]
    private static partial Regex WithOffset();

    // The answers, one per comment id (a repeated id keeps its last reply); each needs an id and a reply.
    public static IReadOnlyList<CodeCommentAnswer> Answers(IReadOnlyList<AnsweredComment>? answered)
    {
        CheckCount(answered?.Count ?? 0, "answered_comments");
        var kept = new Dictionary<Guid, string>();
        for (var i = 0; i < (answered?.Count ?? 0); i++)
        {
            var answer = answered![i] ?? throw ApiException.Validation($"answered_comments[{i}] is empty.", $"answered_comments[{i}]");
            if (answer.Id is not { } id || id == Guid.Empty)
            {
                throw ApiException.Validation($"answered_comments[{i}] needs the id of a code comment.", $"answered_comments[{i}].id");
            }
            if (string.IsNullOrWhiteSpace(answer.Reply))
            {
                throw ApiException.Validation($"answered_comments[{i}] needs a reply.", $"answered_comments[{i}].reply");
            }
            kept[id] = answer.Reply.Trim();
        }
        return [.. kept.Select(k => new CodeCommentAnswer(k.Key, k.Value))];
    }

    // A blocked task goes back with a reason: the constraint it could not meet, or else the first thing it left undone.
    public static string BlockedReason(string? constraintNotMet, IReadOnlyList<string> leftUndone)
        => !string.IsNullOrWhiteSpace(constraintNotMet)
            ? constraintNotMet.Trim()
            : leftUndone.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i))?.Trim()
              ?? throw ApiException.Validation("A blocked report needs its reason: constraint_not_met, or something in left_undone.", "constraint_not_met");

    // report_task's own limit on commits and left-undone items; unrelated to the 100-row cap of the planning tools.
    private const int MaxLines = 25;

    private static void CheckCount(int count, string field)
    {
        if (count > MaxLines)
        {
            throw ApiException.Validation($"Send at most {MaxLines} {field} at a time; you passed {count}.", field);
        }
    }

    private static void CheckNoNulls(IReadOnlyList<string>? lines, string field)
    {
        for (var i = 0; i < (lines?.Count ?? 0); i++)
        {
            if (lines![i] is null)
            {
                throw ApiException.Validation($"{field}[{i}] is empty.", $"{field}[{i}]");
            }
        }
    }

    private static bool IsWebAddress(string? url)
    {
        var trimmed = url?.Trim();
        return trimmed is { Length: > 0 and <= MaxUrlLength } && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    }
}
