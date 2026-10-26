namespace ShipLean.Contracts.Projects;

public sealed record StateChanges(
    IReadOnlyList<StateAdd> Add, IReadOnlyList<StateUpdate> Update, IReadOnlyList<StateReorder> Reorder, IReadOnlyList<StateRemove> Remove);
