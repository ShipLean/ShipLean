using Shouldly;
using static ShipLean.TestSupport.Contracts.Agents.AgentWorld;
using ShipLean.Contracts.Errors;

namespace ShipLean.TestSupport.Contracts.Agents;

// Behaviour every IAgentAuthorizations has: consent records, the "Connected agents" list, revoking.
[TestFixture]
public abstract class AgentAuthorizationsContract
{
    protected abstract AgentWorld World { get; }

    [Test]
    public async Task List_WhenCreated_ShouldShowTheAgent()
    {
        var before = World.Now;
        var (userId, _, authorizationId) = await World.AuthorizeAsync("shiplean:write", "shiplean:read");

        var agents = await World.Authorizations.ListAsync(userId);

        var agent = agents.ShouldHaveSingleItem();
        agent.Id.ShouldBe(authorizationId);
        agent.ClientName.ShouldBe("Contract Agent");
        agent.ClientHost.ShouldBeNull();
        agent.Verified.ShouldBeFalse();
        agent.Scopes.ShouldBe(["shiplean:read", "shiplean:write"]);
        agent.ConnectedAt.ShouldBe(before, TimeSpan.FromSeconds(30));
        agent.LastUsedAt.ShouldBeNull();
    }

    [Test]
    public async Task List_WhenSeveral_ShouldBeNewestFirst()
    {
        var userId = await World.ArrangeUserAsync();
        var first = await World.Authorizations.CreateAsync(userId, await World.RegisterClientAsync(), new HashSet<string> { "shiplean:read" });
        World.Advance(TimeSpan.FromMinutes(5));
        var second = await World.Authorizations.CreateAsync(userId, await World.RegisterClientAsync(), new HashSet<string> { "shiplean:read" });

        (await World.Authorizations.ListAsync(userId)).Select(a => a.Id).ShouldBe([second, first]);
    }

    [Test]
    public async Task List_WhenAnotherUsersAgent_ShouldNotShowIt()
    {
        await World.AuthorizeAsync();

        (await World.Authorizations.ListAsync(await World.ArrangeUserAsync())).ShouldBeEmpty();
    }

    [Test]
    public async Task List_WhenTokenUsed_ShouldShowLastUsed()
    {
        var (userId, _, pair) = await World.IssueAsync();
        World.Advance(TimeSpan.FromMinutes(2));

        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldNotBeNull();

        (await World.Authorizations.ListAsync(userId)).ShouldHaveSingleItem().LastUsedAt!.Value.ShouldBe(World.Now, TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task Revoke_ShouldHideAgentAndKillItsTokens()
    {
        var (userId, authorizationId, pair) = await World.IssueAsync();

        await World.Authorizations.RevokeAsync(userId, authorizationId);

        (await World.Authorizations.ListAsync(userId)).ShouldBeEmpty();
        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldBeNull();
        (await World.RefreshAsync(pair.RefreshToken, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Revoke_ShouldLeaveOtherAgentsAlone()
    {
        var userId = await World.ArrangeUserAsync();
        var first = await World.Authorizations.CreateAsync(userId, await World.RegisterClientAsync(), new HashSet<string> { "shiplean:read" });
        var second = await World.Authorizations.CreateAsync(userId, await World.RegisterClientAsync(), new HashSet<string> { "shiplean:read" });
        var secondPair = await World.Issuer.IssueAsync(second, Resource);

        await World.Authorizations.RevokeAsync(userId, first);

        (await World.Authorizations.ListAsync(userId)).ShouldHaveSingleItem().Id.ShouldBe(second);
        (await World.Tokens.ValidateAsync(secondPair.AccessToken, Resource)).ShouldNotBeNull();
    }

    [Test]
    public async Task Revoke_WhenOtherUsersAuthorization_ShouldBeNotFound()
    {
        var (owner, _, authorizationId) = await World.AuthorizeAsync();
        var other = await World.ArrangeUserAsync();

        (await Should.ThrowAsync<ApiException>(() => World.Authorizations.RevokeAsync(other, authorizationId))).Code.ShouldBe(ApiErrorCodes.NotFound);

        (await World.Authorizations.ListAsync(owner)).ShouldHaveSingleItem();
    }

    [Test]
    public async Task Revoke_WhenUnknown_ShouldBeNotFound()
    {
        var userId = await World.ArrangeUserAsync();

        (await Should.ThrowAsync<ApiException>(() => World.Authorizations.RevokeAsync(userId, Guid.NewGuid()))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Create_WhenClientUnknown_ShouldBeNotFound()
    {
        var userId = await World.ArrangeUserAsync();

        (await Should.ThrowAsync<ApiException>(() => World.Authorizations.CreateAsync(userId, "no-such-client", new HashSet<string> { "shiplean:read" }))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Create_WhenUserUnknown_ShouldBeNotFound()
    {
        var clientId = await World.RegisterClientAsync();

        (await Should.ThrowAsync<ApiException>(() => World.Authorizations.CreateAsync(Guid.NewGuid(), clientId, new HashSet<string> { "shiplean:read" }))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task List_ShouldReturnScopesSorted()
    {
        var (userId, _, _) = await World.AuthorizeAsync("shiplean:write", "shiplean:plan", "shiplean:read");

        (await World.Authorizations.ListAsync(userId)).ShouldHaveSingleItem().Scopes.ShouldBe(["shiplean:plan", "shiplean:read", "shiplean:write"]);
    }

    [Test]
    public async Task Create_WhenLiveAuthorizationExists_ShouldUnionScopesAndReturnSameId()
    {
        var (userId, clientId, first) = await World.AuthorizeAsync("shiplean:read");
        var pair = await World.Issuer.IssueAsync(first, Resource);

        var second = await World.Authorizations.CreateAsync(userId, clientId, new HashSet<string> { "shiplean:write", "shiplean:read" });

        second.ShouldBe(first);
        (await World.Authorizations.ListAsync(userId)).ShouldHaveSingleItem().Scopes.ShouldBe(["shiplean:read", "shiplean:write"]);
        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldNotBeNull();
    }

    [Test]
    public async Task Create_WhenLiveAuthorizationExistsAndStampChanged_ShouldRevokeOldAndCreateNew()
    {
        var (userId, clientId, first) = await World.AuthorizeAsync("shiplean:read", "shiplean:write");
        var old = await World.Issuer.IssueAsync(first, Resource);
        await World.RotateSecurityStampAsync(userId);

        var second = await World.Authorizations.CreateAsync(userId, clientId, new HashSet<string> { "shiplean:read" });

        second.ShouldNotBe(first);
        (await World.Tokens.ValidateAsync(old.AccessToken, Resource)).ShouldBeNull();
        (await World.Issuer.RefreshAsync(old.RefreshToken, clientId, Resource)).ShouldBeNull();
        var listed = (await World.Authorizations.ListAsync(userId)).ShouldHaveSingleItem();
        listed.Id.ShouldBe(second);
        listed.Scopes.ShouldBe(["shiplean:read"]);
        var fresh = await World.Issuer.IssueAsync(second, Resource);
        (await World.Tokens.ValidateAsync(fresh.AccessToken, Resource)).ShouldNotBeNull();
        (await World.Issuer.RefreshAsync(fresh.RefreshToken, clientId, Resource)).ShouldNotBeNull();
    }

    [Test]
    public async Task Create_WhenEarlierAuthorizationRevoked_ShouldMakeANewOne()
    {
        var (userId, clientId, first) = await World.AuthorizeAsync();
        await World.Authorizations.RevokeAsync(userId, first);

        var second = await World.Authorizations.CreateAsync(userId, clientId, new HashSet<string> { "shiplean:read" });

        second.ShouldNotBe(first);
        (await World.Authorizations.ListAsync(userId)).ShouldHaveSingleItem().Id.ShouldBe(second);
    }

    [Test]
    public async Task Create_WhenOtherUserHasLiveAuthorizationForTheClient_ShouldMakeANewOne()
    {
        var (_, clientId, first) = await World.AuthorizeAsync();
        var other = await World.ArrangeUserAsync();

        (await World.Authorizations.CreateAsync(other, clientId, new HashSet<string> { "shiplean:read" })).ShouldNotBe(first);
    }
}
