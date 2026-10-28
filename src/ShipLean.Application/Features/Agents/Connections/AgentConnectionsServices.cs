using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Connections;

public sealed class AgentConnectionsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddSingleton<IAgentConnections, AgentConnections>();
}
