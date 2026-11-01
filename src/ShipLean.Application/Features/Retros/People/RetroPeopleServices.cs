using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

public sealed class RetroPeopleServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<RetroPointWriter>();
        services.AddScoped<RetroActionWriter>();
        services.AddScoped<RetroThemeWriter>();
        services.AddScoped<IRetroPeopleWrites, RetroPeopleWrites>();
    }
}
