using ShipLean.Contracts.Import;

namespace ShipLean.WebApi.Features.Import.Jobs;

// Mapping null re-runs with the mapping the job has saved.
public sealed record ImportRerunRequest(ImportMappingDto? Mapping);
