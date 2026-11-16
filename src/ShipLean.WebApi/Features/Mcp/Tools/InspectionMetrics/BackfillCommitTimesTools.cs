using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Plans;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.InspectionMetrics;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;

namespace ShipLean.WebApi.Features.Mcp.Tools.InspectionMetrics;

// TODO: temporary; remove once existing installs are backfilled
// Instance admins only (anyone else gets not_found from ICommitTimeBackfill). Names no project.
[McpServerToolType]
internal sealed class BackfillCommitTimesTools(ICommitTimeBackfill backfill, IAgentAddresses addresses)
{
    private const int MaxCommits = 500;

    [McpServerTool(Name = "backfill_commit_times"), McpScope(McpScopes.Write)]
    [Description("Instance admins only; temporary. Fills in the time of commit links made before commit times were stored, so lead time can end at a task's last commit. Called with no commits, it lists the commit links that have no time yet (sha7, url, item), 200 a page; pass next_cursor as after for the next page. Called with commits (at most 500, each {sha, committed_at} from git log --format='%H %aI' in the repository the link points to), it sets the time on every commit link whose first 7 sha characters match and whose time is empty, never overwriting one, and returns updated, already_set and not_found. Only commit LINKS need this: commits of readable repositories live in work_item_commit and already get their time from the commit's authored date. See docs/runbooks/backfill-commit-times.md.")]
    public async Task<CallToolResult> BackfillCommitTimes(
        [Description("The commits to set times for, 1 to 500. Leave out to list the links that still have no time.")] IReadOnlyList<CommitTimeInput>? commits = null,
        [Description("When listing: the previous page's next_cursor.")] string? after = null,
        [Description("When listing: links a page, 1 to 500. Default 200.")] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer.TrimEnd('/');
        if (commits is null)
        {
            var page = await backfill.ListMissingAsync(limit, after, cancellationToken);
            var missing = page.Links.Select(link => new MissingCommitLink(link.Sha7, link.Url, link.ItemKey)).ToList();
            return CaptureSupport.Reply(
                new BackfillCommitTimesResult(null, issuer, missing, page.NextCursor, null, null, null),
                $"{missing.Count} commit links without a time{(page.NextCursor is null ? string.Empty : "; more on the next page")}.");
        }

        var counts = await backfill.SetAsync(ParseAll(commits), cancellationToken);
        return CaptureSupport.Reply(
            new BackfillCommitTimesResult(null, issuer, null, null, counts.Updated, counts.AlreadySet, counts.NotFound),
            $"Updated {counts.Updated} links, {counts.AlreadySet} already had a time, {counts.NotFound} shas matched no commit link.");
    }

    private static List<CommitTimeDto> ParseAll(IReadOnlyList<CommitTimeInput> commits)
    {
        if (commits.Count is < 1 or > MaxCommits)
        {
            throw ApiException.Validation($"Send between 1 and {MaxCommits} commits.", "commits");
        }
        return [.. commits.Select(Parse)];
    }

    private static CommitTimeDto Parse(CommitTimeInput? input, int index)
    {
        if (!PlanMarkers.IsCommitSha(input?.Sha))
        {
            throw ApiException.Validation("A commit needs a sha of at least 7 hex characters.", $"commits[{index}].sha");
        }
        if (!DateTimeOffset.TryParse(input!.CommittedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
        {
            throw ApiException.Validation("committed_at must be an ISO 8601 time with an offset.", $"commits[{index}].committed_at");
        }
        return new CommitTimeDto(input.Sha!, at);
    }
}
