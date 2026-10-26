using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Persisted;

public sealed record ColumnStateMapping(WorkItemTypeId Type, Guid StateId);
