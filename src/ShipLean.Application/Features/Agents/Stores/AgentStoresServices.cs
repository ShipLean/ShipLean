using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Stores;

// Singletons: they hold no per-request state (the current user is read at call time).
public sealed class AgentStoresServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IOAuthClients, OAuthClients>();
        services.AddSingleton<IOAuthRequests, OAuthRequests>();
        services.AddSingleton<IAgentConsent, AgentConsent>();
        services.AddSingleton<IAgentAuthorizations, AgentAuthorizations>();
        services.AddSingleton<IOAuthCodes, OAuthCodes>();
        services.AddSingleton<IAgentTokens, AgentTokens>();
        services.AddSingleton<IAgentTokenIssuer, AgentTokenIssuer>();
    }
}
