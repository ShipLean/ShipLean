using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.Import;

public sealed record ImportErrorDto(Guid Id, ImportStep Step, string Entity, string SourceKey, string Message, ImportErrorResolution Resolution);
