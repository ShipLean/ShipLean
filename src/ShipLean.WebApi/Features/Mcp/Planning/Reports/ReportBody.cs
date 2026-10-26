using System.Globalization;
using System.Text;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Planning.Reports;

// The Report comment: the marker line with the status, then each field the agent filled in as a Markdown section.
internal static class ReportBody
{
    public static string Build(ReportStatus status, string? blockedReason, IReadOnlyList<ReportCommit> commits, string? prUrl, ReportFields fields)
    {
        var body = new StringBuilder(PlanMarkers.Report(status.ToString().ToLowerInvariant()));
        Section(body, "Blocked", blockedReason);
        Section(body, "Commits", Bullets(commits.Select(CommitLine)));
        Section(body, "Pull request", prUrl?.Trim());
        Section(body, "Tests run", Bullets(fields.TestsRun));
        Section(body, "Acceptance tests", Bullets(fields.AcceptanceTests));
        Section(body, "Config files changed", fields.ConfigFilesChanged);
        Section(body, "Red tests", fields.RedTests);
        Section(body, "Context used", fields.ContextUsedK is { } k ? $"about {k.ToString(CultureInfo.InvariantCulture)}k tokens" : null);
        Section(body, "Public surface", fields.PublicSurface);
        Section(body, "Decisions", fields.Decisions);
        Section(body, "Gotchas", fields.Gotchas);
        Section(body, "Left undone", Bullets(fields.LeftUndone));
        Section(body, "Temporary behaviour", fields.TemporaryBehaviour);
        if (fields.ConstraintNotMet?.Trim() != blockedReason)
        {
            Section(body, "Constraint not met", fields.ConstraintNotMet);
        }
        return body.ToString();
    }

    private static string CommitLine(ReportCommit commit)
    {
        var subject = string.IsNullOrWhiteSpace(commit.Subject) ? string.Empty : commit.Subject.Trim() + " ";
        return $"`{commit.Sha!.Trim()[..7].ToLowerInvariant()}` {subject}({commit.Url!.Trim()})";
    }

    private static string? Bullets(IEnumerable<string> lines)
    {
        var kept = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => $"- {l.Trim()}").ToList();
        return kept.Count == 0 ? null : string.Join('\n', kept);
    }

    private static void Section(StringBuilder body, string heading, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            body.Append("\n\n## ").Append(heading).Append('\n').Append(text.Trim());
        }
    }
}
