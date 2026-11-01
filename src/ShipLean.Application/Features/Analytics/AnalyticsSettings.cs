using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics;

// TODO: replaced by the real store, which reads and writes the analytics columns of instance_settings, validates the host and site
// key, and sends the test event through the saved provider's sink. Until then the settings live in this singleton's memory and
// nothing is validated.
public sealed class AnalyticsSettings
{
    private AnalyticsSettingsDto _current = new(AnalyticsProvider.None, null, null, false);

    public Task<AnalyticsSettingsDto> GetAsync(CancellationToken cancellationToken) => Task.FromResult(_current);

    public Task<AnalyticsSettingsDto> SaveAsync(AnalyticsSettingsDto settings, CancellationToken cancellationToken)
    {
        _current = settings;
        return Task.FromResult(_current);
    }

    public Task<AnalyticsTestResultDto> TestAsync(CancellationToken cancellationToken) => Task.FromResult(new AnalyticsTestResultDto(false, "Not built yet."));
}
