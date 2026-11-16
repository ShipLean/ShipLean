using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Mcp.Agents;

// A singleton like OrientReads: the caller resolved the item and its access before asking.
public sealed class AgentsMcpReadServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<AgentSessionMcpReads>();
        services.AddSingleton<AgentSessionDetailReader>();
    }
}
