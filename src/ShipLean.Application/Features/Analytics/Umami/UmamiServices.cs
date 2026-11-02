using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics.Umami;

public sealed class UmamiServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAnalyticsSink, UmamiSink>();
    }
}
