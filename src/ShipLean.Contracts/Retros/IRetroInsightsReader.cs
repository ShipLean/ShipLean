namespace ShipLean.Contracts.Retros;

// Retrospectives > Insights.
public interface IRetroInsightsReader
{
    Task<RetroInsightsDto> GetAsync(Guid projectId, RetroRange range, CancellationToken cancellationToken = default);
}
