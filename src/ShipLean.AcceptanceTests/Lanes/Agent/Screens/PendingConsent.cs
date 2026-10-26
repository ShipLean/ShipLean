using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Lanes.Agent.Screens;

// Arranges what /oauth/authorize would leave behind, through the agent interfaces only, so it works on the in-memory
// fakes and on the Postgres stores alike.
internal static class PendingConsent
{
    public const string State = "state-1";

    public static async Task<OAuthClient> RegisterClientAsync(ShipLeanHost host, string name, params string[] redirectUris)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOAuthClients>().RegisterAsync(new OAuthClientRegistration(name, redirectUris));
    }

    public static async Task<Guid> CreateAsync(ShipLeanHost host, TestUser user, string clientId, string redirectUri, params string[] scopes)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOAuthRequests>().CreateAsync(new OAuthRequestCreate(
            clientId, redirectUri, State, "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", scopes, host.BaseUrl + AgentUrls.McpPath,
            user.Id ?? throw new ArgumentException("A request belongs to a user.", nameof(user))));
    }

    public static async Task<IReadOnlyList<ConnectedAgentDto>> ConnectedAsync(ShipLeanHost host, TestUser user)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAgentAuthorizations>().ListAsync(user.Id!.Value);
    }
}
