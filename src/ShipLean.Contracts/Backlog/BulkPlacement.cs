using ShipLean.Contracts.WorkItems;

namespace ShipLean.Contracts.Backlog;

// BeforeItemId only with BatchSlot.Before.
public sealed record BulkPlacement(BatchSlot Slot, Guid? BeforeItemId);
