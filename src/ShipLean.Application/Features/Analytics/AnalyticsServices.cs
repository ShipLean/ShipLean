using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics;

public sealed class AnalyticsServices : IServiceModule
{
    public const string HttpClientName = "analytics";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<AnalyticsSettings>();
        services.AddSingleton<IAnalyticsSettingsReader, AnalyticsSettingsReader>();
        services.AddHttpClient(HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
    }
}
