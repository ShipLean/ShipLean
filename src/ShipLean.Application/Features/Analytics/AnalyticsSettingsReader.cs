using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics;

// TODO: replaced by the reader that builds the target from the saved settings (null while the provider is none).
public sealed class AnalyticsSettingsReader : IAnalyticsSettingsReader
{
    public Task<AnalyticsTarget?> GetTargetAsync(CancellationToken cancellationToken) => Task.FromResult<AnalyticsTarget?>(null);
}
