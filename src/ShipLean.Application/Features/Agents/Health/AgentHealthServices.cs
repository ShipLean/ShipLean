using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Health;

// Singletons: they hold no per-request state.
public sealed class AgentHealthServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAgentConnectionHealth, AgentConnectionHealth>();
        services.AddSingleton<IAgentConnectionWarnings, AgentConnectionWarnings>();
    }
}
