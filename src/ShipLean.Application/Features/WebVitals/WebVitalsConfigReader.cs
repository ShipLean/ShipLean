using Agoda.IoC.Core;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.WebVitals;

// GET /api/usage/web-vitals/config: on only when a provider is set (the reader returns no target for none) and the switch is on.
[RegisterSingleton]
public sealed class WebVitalsConfigReader(IAnalyticsSettingsReader settings)
{
    public async Task<WebVitalsConfigDto> GetAsync(CancellationToken cancellationToken)
    {
        var target = await settings.GetTargetAsync(cancellationToken);
        return new WebVitalsConfigDto(target is { SendWebVitals: true });
    }
}
