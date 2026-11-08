using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.IssueIntake.Rounds;

// The project's items closest to an issue (D10): the search behind the MCP `search` tool, run as the run-as member over the
// issue's title, closed items included, the issue's own item left out. Each keeps only what D17 lets a propose session see: key,
// type, state and title, and its github.com pull request links with their merge commits. Never a description, comment or person.
public sealed class ClosestItems(IServiceScopeFactory scopes, NpgsqlDataSource dataSource)
{
    private sealed record PullRow(string Key, string Url, string? Sha, DateTimeOffset? MergedAt);

    public async Task<IReadOnlyList<AgentRelatedWork>> FindAsync(GitHubIssueRecord issue, Guid runAsUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(issue.Title))
        {
            return [];
        }

        var hits = await SearchAsync(issue, runAsUserId, cancellationToken);
        if (hits.Count == 0)
        {
            return [];
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var pulls = (await connection.QueryAsync<PullRow>(new CommandDefinition(
            """
            SELECT w.key, 'https://' || link_url_key(l.url) AS url, ls.merge_commit_sha AS sha, ls.merged_at AS mergedAt
            FROM work_item_link l
            JOIN work_item w ON w.id = l.source_id
            LEFT JOIN link_state ls ON ls.url_key = link_url_key(l.url)
            WHERE l.source_id = ANY(@ids) AND l.link_type = 'url' AND l.archived_at IS NULL
              AND link_url_key(l.url) ~ '^github\.com/[^/]+/[^/]+/pull/[0-9]+$'
            ORDER BY l.created_at, l.id
            """,
            new { ids = hits.Select(h => h.WorkItemId).ToArray() },
            cancellationToken: cancellationToken))).ToLookup(p => p.Key);

        return [.. hits.Select(hit => new AgentRelatedWork(
            hit.Key,
            JsonNamingPolicy.CamelCase.ConvertName(hit.Type.ToString()),
            hit.State,
            hit.Title,
            [.. pulls[hit.Key].Select(p => p.Url).Distinct(StringComparer.Ordinal)],
            [.. pulls[hit.Key].Where(p => p.Sha is not null).Select(p => new AgentMergeCommit(p.Sha!, p.MergedAt)).Distinct()]))];
    }

    // The ranker reads the caller from the request's principal, so the search runs with the run-as member's (as ImportJobRun does).
    private async Task<IReadOnlyList<SearchResultItem>> SearchAsync(GitHubIssueRecord issue, Guid runAsUserId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var previous = accessor.HttpContext;
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, runAsUserId.ToString())], nameof(ClosestItems))),
        };
        try
        {
            var results = await scope.ServiceProvider.GetRequiredService<ISearchRanker>().SearchAsync(
                new SearchQuery(issue.Title, [issue.ProjectId], IncludeClosed: true, Limit: IAgentSessionStarter.MaxRelatedItems + 1),
                cancellationToken);
            return [.. results.Items.Where(i => i.WorkItemId != issue.WorkItemId).Take(IAgentSessionStarter.MaxRelatedItems)];
        }
        finally
        {
            accessor.HttpContext = previous;
        }
    }
}
