using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.WebVitals;

// IWebVitalsQueue is a factory over the one concrete queue, so the dispatcher drains what Track writes. Register is public so a test
// fixture can opt back into the real queue over the host's RecordingWebVitals (it re-adds only the IWebVitalsQueue line, and the last wins).
public sealed class WebVitalsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<WebVitalsQueue>();
        Register(services);
    }

    public static void Register(IServiceCollection services)
        => services.AddSingleton<IWebVitalsQueue>(sp => sp.GetRequiredService<WebVitalsQueue>());
}
