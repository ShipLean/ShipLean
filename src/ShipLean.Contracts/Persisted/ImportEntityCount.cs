namespace ShipLean.Contracts.Persisted;

public sealed record ImportEntityCount(int Done, int Total, int Failed);
