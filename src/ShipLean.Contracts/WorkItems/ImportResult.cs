namespace ShipLean.Contracts.WorkItems;

public sealed record ImportResult(Guid Id, string Key, int Revision, bool Created);
