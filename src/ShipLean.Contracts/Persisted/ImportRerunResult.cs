namespace ShipLean.Contracts.Persisted;

public sealed record ImportRerunResult(int Updated, int Unchanged, int Added);
