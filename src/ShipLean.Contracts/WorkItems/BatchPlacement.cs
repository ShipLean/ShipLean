namespace ShipLean.Contracts.WorkItems;

// BeforeItemId is set only with BatchSlot.Before. A before item that is gone or in another scope means last.
public sealed record BatchPlacement(BatchSlot Slot, Guid? BeforeItemId);
