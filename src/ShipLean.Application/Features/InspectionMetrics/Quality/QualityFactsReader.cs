using ShipLean.Application.Features.InspectionMetrics.Facts;

namespace ShipLean.Application.Features.InspectionMetrics.Quality;

// TODO: a stub that reads nothing, so the view has no quality yet; the real reader over Postgres replaces it.
public sealed class QualityFactsReader : IQualityFactsReader
{
    public Task<BugFacts> ReadAsync(Guid projectId, DateOnly fromDay, DateOnly toDay, CancellationToken ct)
        => Task.FromResult(new BugFacts([], [], new Dictionary<int, int>(), LeadTimeFacts.Empty));
}
