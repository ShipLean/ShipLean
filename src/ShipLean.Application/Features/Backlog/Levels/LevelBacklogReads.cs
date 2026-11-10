using ShipLean.Application.Features.Backlog.Shared;
using ShipLean.Contracts.Backlog.Levels;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Backlog.Levels;

// The Features or Epics backlog over the tree reads: one stack read of the level carries the filter and includeCompleted, and one
// roll-up read gives every row's counts (story-level items and nested Features at any depth, closed ones included). Tag colours are
// the level's own board's. Tree mode (Features only) lays the same Features out under their Epics and parent Features (LevelBacklogTree).
public sealed class LevelBacklogReads(IWorkTreeReads reads, IBoardSettingsStore boards, BacklogTriage triage, BacklogRowExtras extras) : ILevelBacklogReads
{
    public async Task<BacklogViewDto> GetAsync(Guid projectId, BoardLevel level, BacklogMode mode, string? filter, bool includeCompleted, CancellationToken cancellationToken = default)
    {
        var stackLevel = level switch
        {
            BoardLevel.Features => StackLevel.Feature,
            BoardLevel.Epics => StackLevel.Epic,
            _ => throw ApiException.Validation("The level must be features or epics."),
        };
        if (mode == BacklogMode.Planning || (mode == BacklogMode.Tree && level != BoardLevel.Features))
        {
            throw ApiException.Validation("The Features backlog takes list or tree mode, the Epics backlog list mode.", "mode");
        }
        var criteria = BacklogFilter.Parse(filter);
        var waiting = await triage.ReadAsync(projectId, [DbType(stackLevel)], criteria, cancellationToken);
        var read = waiting.Visible(await reads.StackAsync(projectId, stackLevel, criteria, includeCompleted, cancellationToken));
        var whole = criteria is null ? read : waiting.Visible(await reads.StackAsync(projectId, stackLevel, null, includeCompleted, cancellationToken));
        var rank = whole.Select((item, index) => (item.Id, Rank: index + 1)).ToDictionary(x => x.Id, x => x.Rank);
        // An item that closed or went between the two reads has no rank in the whole read: it is left out.
        var items = read.Where(item => rank.ContainsKey(item.Id)).ToList();
        var factory = new BacklogRows(await TagColoursAsync(projectId, level, cancellationToken));

        if (mode == BacklogMode.Tree)
        {
            // Not hidden by triage: a hidden Triage Feature stays the group header (a Group row showing its state) of a visible Feature under it.
            var epics = await reads.StackAsync(projectId, StackLevel.Epic, null, true, cancellationToken);
            var features = await reads.StackAsync(projectId, StackLevel.Feature, null, true, cancellationToken);
            var layout = LevelBacklogTree.Layout(items, epics, features);
            var treeRollups = await RollupsAsync(projectId, [.. layout.Where(x => x.Row is not null).Select(x => x.Row!.Id)], cancellationToken);
            var treeRows = layout.Select(x =>
            {
                if (x.Row is null)
                {
                    return LevelBacklogTree.NoParent(layout);
                }
                var own = treeRollups[x.Row.Id];
                return x.Matches
                    ? factory.LevelItem(x.Row, x.Depth, rank[x.Row.Id], Shown(own), ChildPoints(own))
                    : factory.Group(x.Row, x.Depth, Shown(own), ChildPoints(own));
            }).ToList();
            return await extras.ApplyAsync(new BacklogViewDto(projectId, level, BacklogMode.Tree, items.Count, treeRows, [], factory.TagColours, null, null, waiting.Count, waiting.StateIds), cancellationToken);
        }

        var rollups = await RollupsAsync(projectId, [.. items.Select(i => i.Id)], cancellationToken);
        var rows = items.Select(item => factory.LevelItem(item, 0, rank[item.Id], Shown(rollups[item.Id]), ChildPoints(rollups[item.Id]))).ToList();
        return await extras.ApplyAsync(new BacklogViewDto(projectId, level, BacklogMode.List, rows.Count, rows, [], factory.TagColours, null, null, waiting.Count, waiting.StateIds), cancellationToken);
    }

    private static string DbType(StackLevel level) => level == StackLevel.Feature ? "feature" : "epic";

    private async Task<ILookup<Guid, WorkTreeRollupDto>> RollupsAsync(Guid projectId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0 ? Array.Empty<WorkTreeRollupDto>().ToLookup(r => r.ParentId) : (await reads.RollupsAsync(projectId, ids, null, cancellationToken)).ToLookup(r => r.ParentId);

    // Tasks are not drawn here; the progress counts story-level items, and Epics count Features.
    private static List<RollupCountDto> Shown(IEnumerable<WorkTreeRollupDto> own) =>
        [.. own.Where(r => r.Type != WorkItemTypeId.Task).Select(RollupCountDto.From)];

    // Everything estimated under the item: story-level items, open and closed, nested Features' included. Null when none is.
    private static decimal? ChildPoints(IEnumerable<WorkTreeRollupDto> own)
    {
        var estimated = own.Where(r => WorkItemTypeRules.IsStoryLevel(r.Type) && r.TotalPoints is not null).ToList();
        return estimated.Count == 0 ? null : estimated.Sum(r => r.TotalPoints);
    }

    private async Task<IReadOnlyList<TagColourJson>> TagColoursAsync(Guid projectId, BoardLevel level, CancellationToken cancellationToken)
    {
        var board = (await boards.ListAsync(projectId, cancellationToken)).First(b => b.Level == level);
        return (await boards.GetAsync(board.Id, cancellationToken)).Settings.TagColours;
    }
}
