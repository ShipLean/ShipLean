using ShipLean.Contracts.Import;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Fakes.Import;

internal sealed class InMemoryImportError
{
    public required Guid Id { get; init; }

    public required ImportStep Step { get; init; }

    public required string Entity { get; init; }

    public required string SourceKey { get; init; }

    public required string Message { get; init; }

    public ImportErrorResolution Resolution { get; set; } = ImportErrorResolution.Pending;
}
