using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views;

public sealed record ColumnStateDto(WorkItemTypeId Type, StateRef State);
