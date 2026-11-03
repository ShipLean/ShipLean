using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Retros.Decide;

public sealed class DecideServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<DecideScopeReader>();
        services.AddScoped<IRetroDecidePlanner, RetroDecidePlanner>();
    }
}
