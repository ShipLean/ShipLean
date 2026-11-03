namespace ShipLean.Contracts.WorkItems;

// New items in the order they should land. Each item's Tasks are created under it: their ParentId is set by the writer.
public sealed record WorkItemBlockCreate(Guid ProjectId, BatchPlacement? Placement, IReadOnlyList<WorkItemBlockCreate.Item> Items)
{
    public sealed record Item(WorkItemCreate Create, IReadOnlyList<WorkItemCreate> Tasks);
}
