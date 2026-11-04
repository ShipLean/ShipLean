using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Backlog;

namespace ShipLean.Application.Features.Mcp.Backlog;

// One singleton, reached as itself by the Backlog tools and as ISprintFeatureReads by other features.
public sealed class BacklogReadsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<BacklogReads>();
        services.AddSingleton<ISprintFeatureReads>(sp => sp.GetRequiredService<BacklogReads>());
    }
}
