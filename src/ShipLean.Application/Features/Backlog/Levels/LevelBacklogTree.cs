using ShipLean.Application.Features.Backlog.Shared;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Backlog.Levels;

// Tree mode of the Features backlog: the Features the view shows (filter and includeCompleted applied) under their Epics and parent
// Features, then a final "No parent" group for Features with no live parent. Epics and parent Features are read unfiltered and with
// completed ones, so a Feature always shows where it sits: an ancestor that the view leaves out comes along as a Group row (an Epic
// always is one), a Feature the view shows is an Item row with its whole-level rank. An Epic or Feature with no shown Feature under it
// is left out. Children follow their parent in stack order, at one more depth (an Epic is 0). A Feature whose parent is Removed sits
// under No parent, though its Parent stays the Removed item. The No parent row counts the shown Features under it.
public static class LevelBacklogTree
{
    // The rows in order: Row is null for the No parent row; Matches says the Feature passes the view (an Item row) rather than being
    // there for what is under it (a Group row).
    public static IReadOnlyList<(StackRowDto? Row, int Depth, bool Matches)> Layout(
        IReadOnlyList<StackRowDto> shown, IReadOnlyList<StackRowDto> epics, IReadOnlyList<StackRowDto> features)
    {
        var nodes = epics.Concat(features).GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());
        // Id -> its live parent's id; a node with no live parent is not in it.
        var parentOf = nodes.Values.Where(n => n.Parent is not null && nodes.ContainsKey(n.Parent.Id)).ToDictionary(n => n.Id, n => n.Parent!.Id);

        var matches = shown.Select(s => s.Id).ToHashSet();
        var kept = new HashSet<Guid>();
        foreach (var id in shown.Select(s => s.Id).Where(nodes.ContainsKey))
        {
            // Walk up until an ancestor is already kept; the kept check also stops a cycle the model should never hold.
            var current = id;
            while (kept.Add(current) && parentOf.TryGetValue(current, out var parent))
            {
                current = parent;
            }
        }

        var order = epics.Concat(features).Select((n, index) => (n.Id, index)).GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().index);
        var keptNodes = kept.Select(id => nodes[id]).OrderBy(n => order[n.Id]).ToList();
        var children = keptNodes.Where(n => parentOf.ContainsKey(n.Id)).ToLookup(n => parentOf[n.Id]);

        var layout = new List<(StackRowDto? Row, int Depth, bool Matches)>();
        void Emit(StackRowDto node, int depth)
        {
            layout.Add((node, depth, node.Type.Id == WorkItemTypeId.Feature && matches.Contains(node.Id)));
            foreach (var child in children[node.Id])
            {
                Emit(child, depth + 1);
            }
        }

        var top = keptNodes.Where(n => !parentOf.ContainsKey(n.Id)).ToList();
        foreach (var epic in top.Where(n => n.Type.Id != WorkItemTypeId.Feature))
        {
            Emit(epic, 0);
        }
        var orphans = top.Where(n => n.Type.Id == WorkItemTypeId.Feature).ToList();
        if (orphans.Count > 0)
        {
            layout.Add((null, 0, false));
            foreach (var orphan in orphans)
            {
                Emit(orphan, 1);
            }
        }
        return layout;
    }

    // The No parent row: the shown Features after it, open and closed.
    public static BacklogRowDto NoParent(IReadOnlyList<(StackRowDto? Row, int Depth, bool Matches)> layout)
    {
        var under = layout.SkipWhile(x => x.Row is not null).Skip(1).Where(x => x.Matches).Select(x => x.Row!).ToList();
        var open = under.Count(f => f.State.Category != StateCategory.Completed);
        return BacklogRows.NoParent([new RollupCountDto(WorkItemTypeId.Feature, open, under.Count - open, 0, 0)], null);
    }
}
