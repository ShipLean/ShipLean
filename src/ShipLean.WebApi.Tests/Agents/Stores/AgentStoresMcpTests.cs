using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Agents.Stores;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Auth;
using ShipLean.TestSupport.Builders;
using ShipLean.WebApi.Tests.Auth;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Agents.Stores;

// /mcp over the real (Postgres) agent stores: the account behind a token is checked on every call.
[TestFixture]
public class AgentStoresMcpTests : McpFixture
{
    [Test]
    public async Task IssueAgentToken_ShouldWorkAgainstRealStores()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        Host.Services.GetRequiredService<IAgentTokens>().ShouldBeOfType<AgentTokens>();
        (await CallToolAsync(token, "probe_whoami")).Status.ShouldBe(HttpStatusCode.OK);
        await using var connection = await OpenDatabaseAsync();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM oauth_token WHERE kind = 'access' AND token_hash = @hash", new { hash })).ShouldBe(1);
    }

    [Test]
    public async Task ConnectedAgents_WhenRevoked_ShouldAnswer401OnNextCall()
    {
        var token = await TokenAsync(TestUser.Mei, ReadOnly, "Revoked Bot");
        (await CallToolAsync(token, "probe_whoami")).Status.ShouldBe(HttpStatusCode.OK);
        var mei = Api.ActingAs(TestUser.Mei);
        var agent = (await mei.GetJsonAsync<List<ConnectedAgentDto>>("api/me/agents")).Where(a => a.ClientName == "Revoked Bot").ShouldHaveSingleItem();

        using var revoke = await mei.DeleteAsync($"api/me/agents/{agent.Id}");

        revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CallToolAsync(token, "probe_whoami")).Status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Mcp_WhenPasswordChanged_ShouldAnswer401()
    {
        var builder = new AppUserBuilder();
        Guid userId;
        await using (var connection = await OpenDatabaseAsync())
        {
            userId = await builder.InsertAsync(connection);
        }
        var token = await TokenAsync(new TestUser(userId), ReadOnly);
        (await CallToolAsync(token, "probe_whoami")).Status.ShouldBe(HttpStatusCode.OK);
        using var browser = await AuthClient.SignedInAsync(Host, builder.Email, AppUserBuilder.ArrangedPassword);

        using var changed = await browser.PostAsJsonAsync("/api/auth/password", new ChangePasswordRequest(AppUserBuilder.ArrangedPassword, "a-brand-new-password"));

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CallToolAsync(token, "probe_whoami")).Status.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
