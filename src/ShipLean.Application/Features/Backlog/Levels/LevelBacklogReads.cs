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
// roll-up read gives every row's counts (stories, bugs and nested Features at any depth, closed ones included). Tag colours are
// the level's own board's.
public sealed class LevelBacklogReads(IWorkTreeReads reads, IBoardSettingsStore boards) : ILevelBacklogReads
{
    public async Task<BacklogViewDto> GetAsync(Guid projectId, BoardLevel level, string? filter, bool includeCompleted, CancellationToken cancellationToken = default)
    {
        var stackLevel = level switch
        {
            BoardLevel.Features => StackLevel.Feature,
            BoardLevel.Epics => StackLevel.Epic,
            _ => throw ApiException.Validation("The level must be features or epics."),
        };
        var criteria = BacklogFilter.Parse(filter);
        var read = await reads.StackAsync(projectId, stackLevel, criteria, includeCompleted, cancellationToken);
        var whole = criteria is null ? read : await reads.StackAsync(projectId, stackLevel, null, includeCompleted, cancellationToken);
        var rank = whole.Select((item, index) => (item.Id, Rank: index + 1)).ToDictionary(x => x.Id, x => x.Rank);
        // An item that closed or went between the two reads has no rank in the whole read: it is left out.
        var items = read.Where(item => rank.ContainsKey(item.Id)).ToList();
        var rollups = (await reads.RollupsAsync(projectId, [.. items.Select(i => i.Id)], null, cancellationToken)).ToLookup(r => r.ParentId);
        var factory = new BacklogRows(await TagColoursAsync(projectId, level, cancellationToken));

        var rows = items.Select(item =>
        {
            var own = rollups[item.Id].ToList();
            return factory.LevelItem(item, rank[item.Id], [.. own.Where(IsShown).Select(r => new RollupCountDto(r.Type, r.Open, r.Closed))], ChildPoints(own));
        }).ToList();
        return new BacklogViewDto(projectId, level, BacklogMode.List, rows.Count, rows, [], factory.TagColours, null, null);
    }

    // Tasks are not drawn here; the progress counts stories and bugs, and Epics count Features.
    private static bool IsShown(WorkTreeRollupDto rollup) => rollup.Type != WorkItemTypeId.Task;

    // Everything estimated under the item: stories and bugs, open and closed, nested Features' included. Null when none is.
    private static decimal? ChildPoints(IEnumerable<WorkTreeRollupDto> own)
    {
        var estimated = own.Where(r => r.Type is WorkItemTypeId.Story or WorkItemTypeId.Bug && r.TotalPoints is not null).ToList();
        return estimated.Count == 0 ? null : estimated.Sum(r => r.TotalPoints);
    }

    private async Task<IReadOnlyList<TagColourJson>> TagColoursAsync(Guid projectId, BoardLevel level, CancellationToken cancellationToken)
    {
        var board = (await boards.ListAsync(projectId, cancellationToken)).First(b => b.Level == level);
        return (await boards.GetAsync(board.Id, cancellationToken)).Settings.TagColours;
    }
}
