using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Backlog.Shared;
using ShipLean.Contracts;
using ShipLean.Contracts.Backlog.Levels;

namespace ShipLean.Application.Features.Backlog.Levels;

public sealed class LevelBacklogServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddScoped<BacklogTriage>();
        services.TryAddScoped<BacklogRowExtras>();
        services.AddScoped<ILevelBacklogReads, LevelBacklogReads>();
    }
}
