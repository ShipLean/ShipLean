namespace ShipLean.Application.Features.InspectionMetrics.Facts;

public sealed class LeadTimeFactsReader : ILeadTimeFactsReader
{
    // TODO: read the real facts from Postgres; until then every window has no facts.
    public Task<LeadTimeFacts> ReadAsync(Guid projectId, DateOnly fromDay, DateOnly toDay, CancellationToken ct) => Task.FromResult(LeadTimeFacts.Empty);
}
