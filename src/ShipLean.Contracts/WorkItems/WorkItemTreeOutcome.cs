namespace ShipLean.Contracts.WorkItems;

// What CreateTreeAsync wrote, in the shape of its input: Items[i] is the tree's Items[i], and each node's Children are its
// input node's Children, in order.
public sealed record WorkItemTreeOutcome(IReadOnlyList<WorkItemTreeOutcome.Node> Items)
{
    public sealed record Node(Guid Id, string Key, IReadOnlyList<Node> Children);
}
