namespace ShipLean.Contracts.Import;

public sealed record ImportedAsDto(string ProjectKey, Guid JobId, DateTimeOffset FinishedAt, string FinishedLabel);
