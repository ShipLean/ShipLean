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
        services.AddScoped<RetroActionDetailReader>();
        services.AddScoped<RetroThemeWriter>();
        services.AddScoped<RetroDecisionWriter>();
        services.AddScoped<RetroMergeProposalWriter>();
        services.AddScoped<IRetroPeopleWrites, RetroPeopleWrites>();
        services.AddScoped<IRetroDecisionWrites, RetroDecisionWrites>();
    }
}
