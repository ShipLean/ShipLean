using Dapper;
using Npgsql;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// IPullRequestLinks: the PR webhook's auto link for a url an agent reports. Same duplicate rule (an archived link counts), same
// webhook write; the link carries no added_via mark, since no webhook matched it.
public sealed class PullRequestLinks(NpgsqlDataSource dataSource, IWorkItemStore store) : IPullRequestLinks
{
    public async Task<bool> LinkAsync(Guid workItemId, string url, string? title, string linkedBy, CancellationToken cancellationToken = default)
    {
        if (PullRequestUrl.Parse(url) is not { } pullRequest)
        {
            return false;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var revision = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            """
            SELECT w.revision FROM work_item w
            WHERE w.id = @workItemId
              AND NOT EXISTS (SELECT 1 FROM work_item_link l
                              WHERE l.source_id = w.id AND l.link_type = 'url' AND link_url_key(l.url) = link_url_key(@url))
            """,
            new { workItemId, url }, cancellationToken: cancellationToken));
        if (revision is null)
        {
            return false;
        }

        var changes = new WorkItemChanges { AddLinks = [new LinkValue(LinkType.Url, null, null, url, string.IsNullOrWhiteSpace(title) ? null : title)] };
        return await LinkWrites.PatchAsync(store, workItemId, revision.Value, changes, WriteOptions.FromWebhook($"{pullRequest.Reference} ({linkedBy})"), cancellationToken);
    }
}
