using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Tools.Agents;

// The get_agent_session reader, a singleton like the MCP resolvers.
public sealed class AgentsMcpServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAgentSessionDetails, StubAgentSessionDetails>();
    }
}
