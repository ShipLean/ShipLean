using ShipLean.Contracts.Import;

namespace ShipLean.Application.Fakes.Import;

internal sealed record InMemoryImportLog(long Id, DateTimeOffset LoggedAt, ImportLogLevel Level, string Message);
