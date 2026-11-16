using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Tools.Agents;

// Singletons like the MCP resolvers: the user is read from the request at call time.
public sealed class AgentsMcpServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<AgentSessionMcpReads>();
        services.AddSingleton<IAgentSessionDetails, StubAgentSessionDetails>();
    }
}
