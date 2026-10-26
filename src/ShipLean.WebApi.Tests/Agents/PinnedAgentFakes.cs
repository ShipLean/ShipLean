using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Agents;
using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Tests.Agents;

// The agent API tests arrange their data through the in-memory agent fakes, so they pin those fakes here rather than rely
// on what the app registers: when the real implementations are registered these tests keep running over the fakes, and the
// real ones' user checks are proven by their inherited contract tests.
internal static class PinnedAgentFakes
{
    public static void Pin(IServiceCollection services)
    {
        services.AddSingleton<InMemoryAgentStore>();
        services.AddSingleton<IOAuthClients, InMemoryOAuthClients>();
        services.AddSingleton<IAgentAuthorizations, InMemoryAgentAuthorizations>();
        services.AddSingleton<IOAuthCodes, InMemoryOAuthCodes>();
        services.AddSingleton<IAgentTokens, InMemoryAgentTokens>();
        services.AddSingleton<IAgentTokenIssuer, InMemoryAgentTokenIssuer>();
        services.AddSingleton<IClientMetadataDocuments, InMemoryClientMetadataDocuments>();
        services.AddSingleton<IOAuthRequests, InMemoryOAuthRequests>();
        services.AddSingleton<InMemoryAgentConsent>();
        services.AddSingleton<IAgentConsent>(sp => sp.GetRequiredService<InMemoryAgentConsent>());
    }
}
