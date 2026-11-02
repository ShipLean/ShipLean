using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics.PostHog;

public sealed class PostHogServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAnalyticsSink>(sp => new PostHogSink(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(AnalyticsServices.HttpClientName),
            sp.GetRequiredService<ILogger<PostHogSink>>()));
    }
}
