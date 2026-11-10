namespace ShipLean.Application.Features.Inbox;

// One Inbox row for ordering: its id, the id of the Feature above it when it is a Story or Bug under a Feature (else null),
// and the earliest of its asks.
public sealed record InboxOrderEntry(Guid Id, Guid? FeatureId, DateTimeOffset OldestAt);

// Where an ordered row stands: UnderId is the Feature's id when the row is a Story or Bug placed under that Feature's row.
public sealed record InboxPlacement(Guid Id, Guid? UnderId);

// The Inbox's row order (D2, D3): longest waiting first. A Story or Bug whose Feature also has a row goes straight after that
// Feature, among its siblings by its own OldestAt, and the pair sits where the earlier of the two would. A Story whose Feature
// has no row stands alone.
public static class InboxOrdering
{
    public static IReadOnlyList<InboxPlacement> Order(IReadOnlyList<InboxOrderEntry> entries)
    {
        var ids = entries.Select(e => e.Id).ToHashSet();
        var childrenOf = entries
            .Where(e => e.FeatureId is { } featureId && ids.Contains(featureId))
            .ToLookup(e => e.FeatureId!.Value);
        var groups = entries
            .Where(e => e.FeatureId is not { } featureId || !ids.Contains(featureId))
            .Select(head =>
            {
                var children = childrenOf[head.Id].OrderBy(c => c.OldestAt).ThenBy(c => c.Id).ToList();
                var sits = children.Count == 0 ? head.OldestAt : new[] { head.OldestAt, children[0].OldestAt }.Min();
                return (Sits: sits, Head: head, Children: children);
            })
            .OrderBy(g => g.Sits)
            .ThenBy(g => g.Head.Id);
        var placed = new List<InboxPlacement>();
        foreach (var (_, head, children) in groups)
        {
            placed.Add(new InboxPlacement(head.Id, null));
            placed.AddRange(children.Select(c => new InboxPlacement(c.Id, head.Id)));
        }
        return placed;
    }
}
