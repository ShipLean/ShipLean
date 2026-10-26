using ShipLean.Application.Features.Backlog.Shared;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Backlog.Stories;

// Tree mode: the stories grouped under Epic, Feature and nested Feature, then a final "No parent" group. The group levels are
// read unfiltered and with completed items (a hidden Feature can still hold open stories); only the stories carry the view's
// filter. A group with no story under it, at any depth, is left out. Each group lists its own stories, then its nested Features.
// A story whose Feature is Removed sits under its nearest live ancestor (its Epic, say), though its Parent stays the Removed Feature.
// Every group row, No parent included, counts the filtered stories, bugs and nested Features beneath it, closed ones whatever
// includeCompleted says, and has no Task entries. Rank is the whole backlog's (see TreeStories).
public static class BacklogTree
{
    public static async Task<IReadOnlyList<BacklogRowDto>> BuildAsync(
        IWorkTreeReads reads, Guid projectId, TreeStories source, CriteriaJson? filter, BacklogRows factory, CancellationToken cancellationToken)
    {
        var stories = source.Visible;
        var epics = await reads.StackAsync(projectId, StackLevel.Epic, null, true, cancellationToken);
        var features = await reads.StackAsync(projectId, StackLevel.Feature, null, true, cancellationToken);
        var groups = epics.Concat(features).ToDictionary(g => g.Id);

        var home = await LiveParentsAsync(reads, projectId, groups, source.WithClosed, cancellationToken);
        var storiesByParent = stories.Where(s => home.ContainsKey(s.Id)).ToLookup(s => home[s.Id]);
        var groupsByParent = groups.Values.Where(g => g.Parent is not null && groups.ContainsKey(g.Parent.Id)).ToLookup(g => g.Parent!.Id);
        var topLevel = epics.Concat(features).Where(g => g.Parent is null || !groups.ContainsKey(g.Parent.Id)).ToList();
        var rank = source.Rank;

        var withStories = new Dictionary<Guid, bool>();
        bool HasStories(StackRowDto group)
        {
            if (withStories.TryGetValue(group.Id, out var known))
            {
                return known;
            }
            var result = storiesByParent[group.Id].Any() || groupsByParent[group.Id].Any(HasStories);
            withStories[group.Id] = result;
            return result;
        }

        var layout = new List<(StackRowDto Row, int Depth, bool IsGroup)>();
        void Emit(StackRowDto group, int depth)
        {
            layout.Add((group, depth, true));
            layout.AddRange(storiesByParent[group.Id].Select(story => (story, depth + 1, false)));
            foreach (var child in groupsByParent[group.Id].Where(HasStories))
            {
                Emit(child, depth + 1);
            }
        }

        foreach (var group in topLevel.Where(HasStories))
        {
            Emit(group, 0);
        }

        var rollups = await RollupsByGroupAsync(reads, projectId, [.. layout.Where(x => x.IsGroup).Select(x => x.Row.Id)], filter, cancellationToken);
        var rows = new List<BacklogRowDto>();
        foreach (var (row, depth, isGroup) in layout)
        {
            if (!isGroup)
            {
                rows.Add(factory.Item(row, depth, rank[row.Id]));
                continue;
            }
            var entries = rollups[row.Id];
            rows.Add(factory.Group(row, depth, [.. entries.Where(e => e.Type != WorkItemTypeId.Task).OrderBy(e => e.Type).Select(e => new RollupCountDto(e.Type, e.Open, e.Closed))], ChildPoints(entries)));
        }

        var orphans = stories.Where(s => !home.ContainsKey(s.Id)).ToList();
        if (orphans.Count > 0)
        {
            var counted = source.WithClosed.Where(s => !home.ContainsKey(s.Id)).ToList();
            rows.Add(BacklogRows.NoParent(OrphanRollups(counted), OrphanPoints(counted)));
            rows.AddRange(orphans.Select(story => factory.Item(story, 1, rank[story.Id])));
        }
        return rows;
    }

    // Story id -> the live group it sits under: its Parent when that is a live group, else the deepest live group above it (a Removed
    // Feature is walked through). A story with no live group above is not in the map.
    private static async Task<Dictionary<Guid, Guid>> LiveParentsAsync(
        IWorkTreeReads reads, Guid projectId, Dictionary<Guid, StackRowDto> groups, IReadOnlyList<StackRowDto> stories, CancellationToken cancellationToken)
    {
        var home = stories.Where(s => s.Parent is not null && groups.ContainsKey(s.Parent.Id)).ToDictionary(s => s.Id, s => s.Parent!.Id);
        var lost = stories.Where(s => s.Parent is not null && !home.ContainsKey(s.Id)).Select(s => s.Id).ToHashSet();
        if (lost.Count == 0)
        {
            return home;
        }
        var best = new Dictionary<Guid, (Guid Group, int Size)>();
        foreach (var group in groups.Keys)
        {
            var below = (await reads.DescendantsAsync(projectId, group, null, cancellationToken)).Select(d => d.Id).ToList();
            foreach (var id in below.Where(lost.Contains))
            {
                if (!best.TryGetValue(id, out var known) || below.Count < known.Size)
                {
                    best[id] = (group, below.Count);
                }
            }
        }
        foreach (var (id, found) in best)
        {
            home[id] = found.Group;
        }
        return home;
    }

    private static async Task<Dictionary<Guid, List<WorkTreeRollupDto>>> RollupsByGroupAsync(
        IWorkTreeReads reads, Guid projectId, IReadOnlyCollection<Guid> groupIds, CriteriaJson? filter, CancellationToken cancellationToken)
    {
        var byGroup = groupIds.ToDictionary(id => id, _ => new List<WorkTreeRollupDto>());
        if (groupIds.Count == 0)
        {
            return byGroup;
        }
        foreach (var entry in await reads.RollupsAsync(projectId, groupIds, filter, cancellationToken))
        {
            byGroup[entry.ParentId].Add(entry);
        }
        return byGroup;
    }

    // The points of the stories and bugs under the group, open and closed, as on the boards and the Features backlog (tasks and
    // Features add no points to the sum).
    private static decimal? ChildPoints(IEnumerable<WorkTreeRollupDto> entries) =>
        SumOrNull(entries.Where(e => e.Type is WorkItemTypeId.Story or WorkItemTypeId.Bug).Select(e => e.TotalPoints));

    private static List<RollupCountDto> OrphanRollups(IEnumerable<StackRowDto> orphans) =>
        [.. orphans.GroupBy(s => s.Type.Id).OrderBy(g => g.Key)
            .Select(g => new RollupCountDto(g.Key, g.Count(s => s.State.Category != StateCategory.Completed), g.Count(s => s.State.Category == StateCategory.Completed)))];

    private static decimal? OrphanPoints(IEnumerable<StackRowDto> orphans) =>
        SumOrNull(orphans.Select(s => s.Points));

    // Nothing estimated is not zero points.
    private static decimal? SumOrNull(IEnumerable<decimal?> points)
    {
        var estimated = points.OfType<decimal>().ToList();
        return estimated.Count == 0 ? null : estimated.Sum();
    }
}
