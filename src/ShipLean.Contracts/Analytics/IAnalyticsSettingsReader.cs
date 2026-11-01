namespace ShipLean.Contracts.Analytics;

public interface IAnalyticsSettingsReader
{
    // Null when the provider is none.
    Task<AnalyticsTarget?> GetTargetAsync(CancellationToken cancellationToken);
}
