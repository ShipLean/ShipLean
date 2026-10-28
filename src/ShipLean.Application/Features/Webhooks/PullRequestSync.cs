using System.Data.Common;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// What a pull or merge request event does, the same for every provider:
//   1. links it to each Story, Task or Bug whose key its title or source branch names, unless that item has (or had,
//      before someone archived it) a link to it;
//   2. records its state for the link cards, when any item links to it;
//   3. on a merge or a close, moves each linked item that isn't Completed or Removed to its type's first Completed state,
//      once every pull or merge request the item links to is merged or closed and at least one of them merged.
// Writes go through IWorkItemStore as webhook writes, so history names the pull or merge request and the board's live
// updates show them. A stale revision (someone saved the item meanwhile) is read again and retried.
[RegisterPerRequest]
public sealed class PullRequestSync(NpgsqlDataSource dataSource, IWorkItemStore store, IProjectEvents events, TimeProvider time)
{
    private const int Attempts = 3;

    public async Task<WebhookResultDto> ApplyAsync(PullRequestEvent pullRequest, CancellationToken cancellationToken)
    {
        var linked = await AutoLinkAsync(pullRequest, cancellationToken);
        await RecordStateAsync(pullRequest, cancellationToken);
        var closed = pullRequest.State is LinkCardState.Merged or LinkCardState.Closed ? await CloseAsync(pullRequest, cancellationToken) : [];
        return new WebhookResultDto(WebhookOutcome.Processed, linked, closed);
    }

    private async Task<IReadOnlyList<string>> AutoLinkAsync(PullRequestEvent pullRequest, CancellationToken cancellationToken)
    {
        var keys = TicketKeys.In(pullRequest.Title, pullRequest.SourceBranch);
        if (keys.Count == 0)
        {
            return [];
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var tickets = await connection.QueryAsync<TicketRow>(new CommandDefinition(
            """
            SELECT w.id, w.key, w.revision FROM work_item w
            WHERE w.key = ANY(@keys) AND w.type IN ('story', 'task', 'bug')
              AND NOT EXISTS (SELECT 1 FROM work_item_link l
                              WHERE l.source_id = w.id AND l.link_type = 'url' AND link_url_key(l.url) = link_url_key(@url))
            ORDER BY w.key
            """,
            new { keys = keys.Select(k => k.Key).ToArray(), url = pullRequest.Url }, cancellationToken: cancellationToken));

        var linked = new List<string>();
        foreach (var ticket in tickets)
        {
            var matchedOn = keys.First(k => k.Key == ticket.Key).MatchedOn;
            var changes = new WorkItemChanges { AddLinks = [new LinkValue(LinkType.Url, null, null, pullRequest.Url, pullRequest.Title)] };
            var options = WriteOptions.FromWebhook(pullRequest.Reference, (transaction, ct) => MarkAutoLinkAsync(transaction, ticket.Id, pullRequest, matchedOn, ct));
            if (await PatchAsync(ticket.Id, ticket.Revision, changes, options, cancellationToken))
            {
                linked.Add(ticket.Key);
            }
        }
        return linked;
    }

    // Inside the store's transaction: the link it just added is the webhook's, found by that.
    private static Task MarkAutoLinkAsync(DbTransaction transaction, Guid itemId, PullRequestEvent pullRequest, AutoLinkMatch matchedOn, CancellationToken cancellationToken)
        => transaction.Connection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE work_item_link SET added_via = @via, matched_on = @matchedOn
            WHERE source_id = @itemId AND link_type = 'url' AND archived_at IS NULL AND created_by IS NULL AND added_via IS NULL
              AND link_url_key(url) = link_url_key(@url)
            """,
            new { itemId, via = Db(pullRequest.Provider), matchedOn = Db(matchedOn), url = pullRequest.Url }, transaction, cancellationToken: cancellationToken));

    // Only for a pull or merge request some item links to, so subscribing a busy org stores nothing for unlinked ones.
    // An event older than the stored one changes nothing and publishes nothing; a change publishes linksChanged once for each
    // work item with a live link to it, so open Feature pages refetch their cards.
    private async Task RecordStateAsync(PullRequestEvent pullRequest, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<ChangedItem>(new CommandDefinition(
            """
            WITH changed AS (
            INSERT INTO link_state (url_key, url, state, title, as_of, source, created_at, updated_at)
            SELECT link_url_key(@url), @url, @state, @title, @asOf, @source, @now, @now
            WHERE EXISTS (SELECT 1 FROM work_item_link l
                          WHERE l.link_type = 'url' AND l.archived_at IS NULL AND link_url_key(l.url) = link_url_key(@url))
            ON CONFLICT (url_key) DO UPDATE
                SET url = EXCLUDED.url, state = EXCLUDED.state, title = EXCLUDED.title, as_of = EXCLUDED.as_of,
                    source = EXCLUDED.source, updated_at = EXCLUDED.updated_at
                WHERE link_state.as_of <= EXCLUDED.as_of
            RETURNING url_key)
            SELECT DISTINCT l.source_id AS workItemId, w.project_id AS projectId
            FROM changed c
            JOIN work_item_link l ON l.link_type = 'url' AND l.archived_at IS NULL AND link_url_key(l.url) = c.url_key
            JOIN work_item w ON w.id = l.source_id
            """,
            new
            {
                url = pullRequest.Url,
                state = Db(pullRequest.State),
                title = pullRequest.Title,
                asOf = pullRequest.AsOf,
                source = Db(pullRequest.Provider),
                now = time.GetUtcNow(),
            },
            cancellationToken: cancellationToken));
        foreach (var item in items)
        {
            await events.PublishAsync(new LinksChanged(item.ProjectId, item.WorkItemId));
        }
    }

    private async Task<IReadOnlyList<string>> CloseAsync(PullRequestEvent pullRequest, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> ids;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            ids = [.. await connection.QueryAsync<Guid>(new CommandDefinition(
                """
                SELECT DISTINCT l.source_id FROM work_item_link l
                WHERE l.link_type = 'url' AND l.archived_at IS NULL AND link_url_key(l.url) = link_url_key(@url)
                """,
                new { url = pullRequest.Url }, cancellationToken: cancellationToken))];
        }

        var closed = new List<string>();
        foreach (var id in ids)
        {
            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                var candidate = await CloseCandidateAsync(id, cancellationToken);
                if (candidate is null)
                {
                    break;
                }

                var changes = new WorkItemChanges { State = new FieldSet<Guid>(candidate.DoneStateId) };
                var outcome = await store.PatchAsync(id, new WorkItemPatch(candidate.Revision, changes), WriteOptions.FromWebhook(pullRequest.Reference), cancellationToken);
                if (outcome.Written is not null)
                {
                    closed.Add(candidate.Key);
                    break;
                }
            }
        }
        return closed;
    }

    // The item's key, revision and its type's first Completed state when the close rule says to close it now; null when
    // it is already Completed or Removed, a linked pull or merge request is still open or unknown, none merged, or the
    // type has no Completed state.
    private async Task<CloseRow?> CloseCandidateAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CloseRow>(new CommandDefinition(
            """
            SELECT w.key, w.revision, done.id AS doneStateId
            FROM work_item w
            JOIN state s ON s.id = w.state_id
            CROSS JOIN LATERAL (
                SELECT d.id FROM state d
                WHERE d.project_id = w.project_id AND d.type = w.type AND d.category = 'completed' AND d.archived_at IS NULL
                ORDER BY d.sort_order, d.id LIMIT 1) done
            CROSS JOIN LATERAL (
                SELECT count(*) FILTER (WHERE ls.state IS NULL OR ls.state = 'open')::int AS pending,
                       count(*) FILTER (WHERE ls.state = 'merged')::int AS merged
                FROM work_item_link l LEFT JOIN link_state ls ON ls.url_key = link_url_key(l.url)
                WHERE l.source_id = w.id AND l.link_type = 'url' AND l.archived_at IS NULL
                  AND link_url_key(l.url) ~ '/(pull|merge_requests)/[0-9]+$') pulls
            WHERE w.id = @id AND s.category NOT IN ('completed', 'removed') AND pulls.pending = 0 AND pulls.merged > 0
            """,
            new { id }, cancellationToken: cancellationToken));
    }

    private Task<bool> PatchAsync(Guid id, int revision, WorkItemChanges changes, WriteOptions options, CancellationToken cancellationToken)
        => LinkWrites.PatchAsync(store, id, revision, changes, options, cancellationToken);

    private static string Db<T>(T value) where T : struct, Enum => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private sealed record ChangedItem(Guid WorkItemId, Guid ProjectId);

    private sealed record TicketRow(Guid Id, string Key, int Revision);

    private sealed record CloseRow(string Key, int Revision, Guid DoneStateId);
}
