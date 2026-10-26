using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Planning.Reports;

// What report_task checks before it writes anything, so a call that is refused leaves nothing behind.
internal static class ReportChecks
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
            if (!IsWebAddress(commits[i].Url))
            {
                throw ApiException.Validation($"commits[{i}] needs a url that starts with http:// or https://.", $"commits[{i}].url");
            }
        }

        if (prUrl is not null && !IsWebAddress(prUrl))
        {
            throw ApiException.Validation("pr_url must start with http:// or https://.", "pr_url");
        }
    }

    // A blocked task goes back with a reason: the constraint it could not meet, or else the first thing it left undone.
    public static string BlockedReason(string? constraintNotMet, IReadOnlyList<string> leftUndone)
        => !string.IsNullOrWhiteSpace(constraintNotMet)
            ? constraintNotMet.Trim()
            : leftUndone.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i))?.Trim()
              ?? throw ApiException.Validation("A blocked report needs its reason: constraint_not_met, or something in left_undone.", "constraint_not_met");

    private static void CheckCount(int count, string field)
    {
        if (count > CaptureSupport.MaxItems)
        {
            throw ApiException.Validation($"Send at most {CaptureSupport.MaxItems} {field} at a time; you passed {count}.", field);
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
