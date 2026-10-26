using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Backlog.Levels;

namespace ShipLean.Application.Features.Backlog.Levels;

public sealed class LevelBacklogServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddScoped<ILevelBacklogReads, LevelBacklogReads>();
}
