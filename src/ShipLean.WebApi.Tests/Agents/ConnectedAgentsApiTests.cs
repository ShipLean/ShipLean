using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Agents;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Agents;

// "Connected agents": each test uses users of its own, since the in-memory authorizations are shared by the whole class.
[TestFixture]
public class ConnectedAgentsApiTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => PinnedAgentFakes.Pin;

    private Task<Guid> ConnectAsync(TestUser user, params string[] scopes)
        => Host.Services.GetRequiredService<IAgentAuthorizations>().CreateAsync(
            user.Id!.Value, InMemoryAgentStore.ClaudeCode.ClientId, new HashSet<string>(scopes.Length == 0 ? ["shiplean:read"] : scopes));

    [Test]
    public async Task ConnectedAgents_WhenListed_ShouldShowMineWithTheVerifiedHost()
    {
        var id = await ConnectAsync(TestUser.Mei, "shiplean:read", "shiplean:write");

        var agent = (await Api.ActingAs(TestUser.Mei).GetJsonAsync<List<ConnectedAgentDto>>("api/me/agents")).ShouldHaveSingleItem();

        agent.Id.ShouldBe(id);
        agent.ClientName.ShouldBe("Claude Code");
        agent.ClientHost.ShouldBe("claude.ai");
        agent.Verified.ShouldBeTrue();
        agent.Scopes.ShouldBe(["shiplean:read", "shiplean:write"]);
        agent.LastUsedAt.ShouldBeNull();
        agent.LastUsedLabel.ShouldBeNull();
        agent.ConnectedLabel.ShouldBe(ShipLean.Core.DisplayDate.Relative(agent.ConnectedAt, TimeProvider.System));
    }

    [Test]
    public async Task ConnectedAgents_WhenRevoked_ShouldDropFromList()
    {
        var kai = Api.ActingAs(TestUser.Kai);
        var id = await ConnectAsync(TestUser.Kai);

        using var response = await kai.DeleteAsync($"api/me/agents/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await kai.GetJsonAsync<List<ConnectedAgentDto>>("api/me/agents")).ShouldBeEmpty();
    }

    [Test]
    public async Task ConnectedAgents_WhenOtherUsersAgent_ShouldBeNotFound()
    {
        var id = await ConnectAsync(TestUser.Sam);

        using var response = await Api.ActingAs(TestUser.Tom).DeleteAsync($"api/me/agents/{id}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await Api.ActingAs(TestUser.Sam).GetJsonAsync<List<ConnectedAgentDto>>("api/me/agents")).ShouldHaveSingleItem().Id.ShouldBe(id);
        (await Api.ActingAs(TestUser.Tom).GetJsonAsync<List<ConnectedAgentDto>>("api/me/agents")).ShouldBeEmpty();
    }

    [Test]
    public async Task ConnectedAgents_WhenUnknownId_ShouldBeNotFound()
    {
        using var response = await Api.DeleteAsync($"api/me/agents/{Guid.NewGuid()}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task ConnectedAgents_WhenAnonymous_ShouldAnswer401()
    {
        var anonymous = Api.ActingAs(TestUser.Anonymous);

        using var list = await anonymous.GetAsync("api/me/agents");
        using var revoke = await anonymous.DeleteAsync($"api/me/agents/{Guid.NewGuid()}");

        (await ApiClient.ReadApiErrorAsync(list, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
        (await ApiClient.ReadApiErrorAsync(revoke, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
    }
}
