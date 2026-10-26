using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Search.Index.Wake;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Search.Index;

// The one hosted service that keeps the search index current. Test hosts remove it by default (ShipLeanHost).
public sealed class SearchIndexHostedServiceServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<SearchWakeSources>();
        services.AddHostedService<SearchIndexHostedService>();
    }
}
