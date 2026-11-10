using Dapper;
using Npgsql;
using ShipLean.Application.WorkItems.History;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Backlog.Shared;

// What a backlog view's item rows carry that the tree reads don't: a Feature's health, the open blocked-by keys, what waits on the
// caller, and a story's task counts. One health-and-blockers query, one NeedsYou call and one roll-up read per view, over the rows of
// the view and of every planning section. Group and NoParentGroup rows are left as they are.
public sealed class BacklogRowExtras(NpgsqlDataSource dataSource, INeedsYouCounts counts, ICurrentUser currentUser, IWorkTreeReads reads)
{
    public async Task<BacklogViewDto> ApplyAsync(BacklogViewDto view, CancellationToken cancellationToken)
    {
        var items = view.Rows.Concat(view.Sections.SelectMany(s => s.Rows)).Where(IsItem).ToList();
        if (items.Count == 0)
        {
            return view;
        }
        var ids = items.Select(r => r.Id).Distinct().ToList();
        var stored = await ReadAsync(ids, cancellationToken);
        var waiting = await counts.ForItemsAsync(currentUser.Id, ids, cancellationToken);
        var storyIds = items.Where(r => r.Type is { } t && WorkItemTypeRules.IsStoryLevel(t.Id)).Select(r => r.Id).Distinct().ToList();
        var tasks = storyIds.Count == 0
            ? Array.Empty<WorkTreeRollupDto>().ToLookup(r => r.ParentId)
            : (await reads.RollupsAsync(view.ProjectId, storyIds, null, cancellationToken)).Where(r => r.Type == WorkItemTypeId.Task).ToLookup(r => r.ParentId);

        BacklogRowDto Fill(BacklogRowDto row)
        {
            if (!IsItem(row))
            {
                return row;
            }
            var own = stored[row.Id];
            return row with
            {
                Health = row.Type?.Id == WorkItemTypeId.Feature ? WorkItemFieldView.ParseHealth(own.Health) : null,
                NeedsYou = waiting.GetValueOrDefault(row.Id),
                BlockedBy = own.BlockedBy,
                Rollups = tasks.Contains(row.Id) ? [.. tasks[row.Id].Select(RollupCountDto.From)] : row.Rollups,
            };
        }

        return view with
        {
            Rows = [.. view.Rows.Select(Fill)],
            Sections = [.. view.Sections.Select(s => s with { Rows = [.. s.Rows.Select(Fill)] })],
        };
    }

    private static bool IsItem(BacklogRowDto row) => row.Kind == BacklogRowKind.Item;

    private async Task<Dictionary<Guid, Stored>> ReadAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<StoredRow>(new CommandDefinition(
            """
            SELECT w.id, w.health,
                   COALESCE((SELECT array_agg(t.key ORDER BY t.key)
                             FROM work_item_link l
                             JOIN work_item t ON t.id = l.target_id
                             JOIN state ts ON ts.id = t.state_id
                             WHERE l.source_id = w.id AND l.link_type = 'blockedBy' AND l.archived_at IS NULL
                               AND ts.category NOT IN ('completed', 'removed')), '{}') AS blockedBy
            FROM work_item w
            WHERE w.id = ANY(@ids)
            """,
            new { ids = ids.ToArray() }, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.Id, r => new Stored(r.Health, (string[])r.BlockedBy));
    }

    // BlockedBy is an Array: Npgsql reports text[] as one, and it is always a string[].
    private sealed record StoredRow(Guid Id, string? Health, Array BlockedBy);

    private sealed record Stored(string? Health, IReadOnlyList<string> BlockedBy);
}
