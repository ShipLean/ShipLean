using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Agents;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Lanes.Agent.Screens;

// A verified host: a metadata-document client arranged in the test database, next to a registered (unverified) one.
[TestFixture]
public class ConsentHostTests : SeededAcceptanceTest
{
    private const string VerifiedRedirect = "https://claude.ai/api/mcp/auth_callback";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddSingleton<IAgentAddresses, FixedAgentAddresses>();

    [Test]
    public async Task Consent_ShouldShowHostOrUnverified()
    {
        string verifiedId;
        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            verifiedId = await new OAuthClientBuilder()
                .WithName("Claude Code")
                .WithMetadataDocument("claude.ai", TimeProvider.System.GetUtcNow().AddHours(1))
                .WithRedirectUris(VerifiedRedirect)
                .InsertAsync(connection);
        }
        var verifiedRequest = await PendingConsent.CreateAsync(Host, TestUser.Joel, verifiedId, VerifiedRedirect, "shiplean:read");
        var consent = new ConsentScreen(Page);
        await consent.OpenAsync(verifiedRequest);

        await Expect(consent.Client).ToHaveTextAsync("Claude Code");
        await Expect(consent.Host).ToHaveTextAsync("claude.ai");
        await Expect(consent.Host).ToHaveAttributeAsync("data-verified", "true");

        var unverified = await PendingConsent.RegisterClientAsync(Host, "Claude Code", "https://agent.example.test/callback");
        var unverifiedRequest = await PendingConsent.CreateAsync(Host, TestUser.Joel, unverified.ClientId, "https://agent.example.test/callback", "shiplean:read");
        await consent.OpenAsync(unverifiedRequest);

        await Expect(consent.Client).ToHaveTextAsync("Claude Code");
        await Expect(consent.Host).ToHaveTextAsync("unverified");
        await Expect(consent.Host).ToHaveAttributeAsync("data-verified", "false");
    }
}
