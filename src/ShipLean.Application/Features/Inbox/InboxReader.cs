using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Inbox;
using ShipLean.Core;

namespace ShipLean.Application.Features.Inbox;

public sealed class InboxReader(
    NpgsqlDataSource dataSource, ICurrentUser currentUser, TimeProvider clock, IEnumerable<IInboxAskSource> sources) : IInboxReader
{
    private const int MaxWaitedOnBy = 3;

    public async Task<InboxDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id;
        var now = clock.GetUtcNow();
        var rows = new List<InboxAskRow>();
        var counts = new Dictionary<Guid, int>();
        var ever = false;
        foreach (var source in sources)
        {
            rows.AddRange(await source.OpenAsync(userId, projectId, cancellationToken));
            foreach (var (otherProjectId, count) in await source.OpenCountsByProjectAsync(userId, cancellationToken))
            {
                counts[otherProjectId] = counts.GetValueOrDefault(otherProjectId) + count;
            }
            ever |= await source.EverAsync(userId, cancellationToken);
        }

        var asksByItem = rows.GroupBy(r => r.WorkItemId).ToDictionary(g => g.Key, g => g.Select(r => r.Ask).ToList());
        var itemIds = asksByItem.Keys.ToArray();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var items = (await connection.QueryAsync<ItemRow>(new CommandDefinition(
            "SELECT id, key, type, title, revision, parent_id AS parentId, parent_type AS parentType FROM work_item WHERE id = ANY(@itemIds)",
            new { itemIds }, cancellationToken: cancellationToken))).ToDictionary(i => i.Id);
        var blocked = await WaitedOnByAsync(connection, itemIds, cancellationToken);
        var otherProjectKeys = await ProjectKeysAsync(connection, counts, projectId, cancellationToken);

        var asks = itemIds.ToDictionary(id => id, id => InOrder(asksByItem[id]));
        var entries = itemIds.Select(id => new InboxOrderEntry(
            id,
            items[id].Type is "story" or "bug" && items[id].ParentType == "feature" ? items[id].ParentId : null,
            asks[id].Min(a => a.At))).ToList();
        var dtos = InboxOrdering.Order(entries).Select(placement =>
        {
            var item = items[placement.Id];
            var oldestAt = asks[item.Id].Min(a => a.At);
            var waiting = blocked.GetValueOrDefault(item.Id) ?? [];
            return new InboxItemDto(
                item.Id, item.Key, item.Type, item.Title, item.Revision,
                placement.UnderId is { } under ? items[under].Key : null,
                oldestAt, DisplayDate.Age(oldestAt, now), now - oldestAt > InboxRules.LongWaitAfter,
                [.. waiting.Take(MaxWaitedOnBy)], Math.Max(0, waiting.Count - MaxWaitedOnBy), asks[item.Id]);
        }).ToList();

        return new InboxDto(
            projectId, rows.Count, dtos.Count, ever, DisplayDate.Clock(now, clock), dtos,
            [.. otherProjectKeys.OrderBy(p => p.ProjectKey, StringComparer.Ordinal)]);
    }

    // Questions by number, decisions by number, mentions oldest first labelled @1, @2, then the status line.
    private static List<InboxAskDto> InOrder(List<InboxAskDto> asks)
    {
        var mentions = asks.Where(a => a.Kind == InboxAskKind.Mention).OrderBy(a => a.At).ThenBy(a => a.Mention!.MentionId)
            .Select((a, index) => a with { Label = $"@{index + 1}" });
        return
        [
            .. asks.Where(a => a.Kind == InboxAskKind.Question).OrderBy(a => a.Question!.Number),
            .. asks.Where(a => a.Kind == InboxAskKind.Decision).OrderBy(a => a.Decision!.Number),
            .. mentions,
            .. asks.Where(a => a.Kind == InboxAskKind.Status),
        ];
    }

    // The open items each item blocks: a live blockedBy link whose source is blocked by the item.
    private static async Task<Dictionary<Guid, List<InboxRefDto>>> WaitedOnByAsync(
        NpgsqlConnection connection, Guid[] itemIds, CancellationToken cancellationToken)
    {
        var blocked = await connection.QueryAsync<(Guid BlockerId, string Key, string Title)>(new CommandDefinition(
            """
            SELECT link.target_id, waiting.key, waiting.title
            FROM work_item_link link
            JOIN work_item waiting ON waiting.id = link.source_id
            JOIN state waiting_state ON waiting_state.id = waiting.state_id
            WHERE link.link_type = 'blockedBy' AND link.archived_at IS NULL AND link.target_id = ANY(@itemIds)
              AND waiting_state.category NOT IN ('completed', 'removed')
            ORDER BY waiting.project_id, waiting.number
            """,
            new { itemIds }, cancellationToken: cancellationToken));
        return blocked.GroupBy(b => b.BlockerId).ToDictionary(g => g.Key, g => g.Select(b => new InboxRefDto(b.Key, b.Title)).ToList());
    }

    private static async Task<List<InboxOtherProjectDto>> ProjectKeysAsync(
        NpgsqlConnection connection, Dictionary<Guid, int> counts, Guid projectId, CancellationToken cancellationToken)
    {
        var others = counts.Where(c => c.Key != projectId && c.Value > 0).ToDictionary(c => c.Key, c => c.Value);
        var projectIds = others.Keys.ToArray();
        var keys = await connection.QueryAsync<(Guid Id, string Key)>(new CommandDefinition(
            "SELECT id, key FROM project WHERE id = ANY(@projectIds)", new { projectIds }, cancellationToken: cancellationToken));
        return [.. keys.Select(k => new InboxOtherProjectDto(k.Id, k.Key, others[k.Id]))];
    }

    private sealed record ItemRow(Guid Id, string Key, string Type, string Title, int Revision, Guid? ParentId, string? ParentType);
}
