using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.WorkItems;

public sealed record LinkSnapshot(Guid Id, LinkValue Value);
