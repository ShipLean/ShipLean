using Shouldly;
using ShipLean.Contracts.Errors;
using static ShipLean.TestSupport.Contracts.Agents.AgentWorld;

namespace ShipLean.TestSupport.Contracts.Agents;

// Behaviour every IAgentTokens has: what "a valid agent token" means.
[TestFixture]
public abstract class AgentTokensContract
{
    protected abstract AgentWorld World { get; }

    [Test]
    public async Task Validate_WhenIssued_ShouldReturnPrincipal()
    {
        var (userId, clientId, authorizationId) = await World.AuthorizeAsync("shiplean:read", "shiplean:write");
        var pair = await World.Issuer.IssueAsync(authorizationId, Resource);

        var principal = await World.Tokens.ValidateAsync(pair.AccessToken, Resource);

        principal.ShouldNotBeNull();
        principal.UserId.ShouldBe(userId);
        principal.AuthorizationId.ShouldBe(authorizationId);
        principal.ClientId.ShouldBe(clientId);
        principal.ClientName.ShouldBe("Contract Agent");
        principal.Verified.ShouldBeFalse();
        principal.ClientHost.ShouldBeNull();
        principal.Scopes.ShouldBe(["shiplean:read", "shiplean:write"], ignoreOrder: true);
    }

    [Test]
    public async Task Validate_WhenUnknown_ShouldReturnNull()
    {
        (await World.Tokens.ValidateAsync("not-a-token", Resource)).ShouldBeNull();
        (await World.Tokens.ValidateAsync(string.Empty, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Validate_WhenRefreshTokenPresented_ShouldReturnNull()
    {
        var (_, _, pair) = await World.IssueAsync();

        (await World.Tokens.ValidateAsync(pair.RefreshToken, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Validate_WhenJustBeforeExpiry_ShouldReturnPrincipal()
    {
        var (_, _, pair) = await World.IssueAsync();
        World.Advance(TimeSpan.FromMinutes(59));

        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldNotBeNull();
    }

    [Test]
    public async Task Validate_WhenExpired_ShouldReturnNull()
    {
        var (_, _, pair) = await World.IssueAsync();
        World.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));

        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Validate_WhenRevoked_ShouldReturnNull()
    {
        var (userId, authorizationId, pair) = await World.IssueAsync();
        await World.Authorizations.RevokeAsync(userId, authorizationId);

        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Validate_WhenUserDisabled_ShouldReturnNull()
    {
        var (userId, _, pair) = await World.IssueAsync();
        await World.DisableUserAsync(userId);

        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Validate_WhenSecurityStampChanged_ShouldReturnNull()
    {
        var (userId, _, pair) = await World.IssueAsync();
        await World.RotateSecurityStampAsync(userId);

        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Validate_WhenOtherUsersStampChanged_ShouldReturnPrincipal()
    {
        var (_, _, pair) = await World.IssueAsync();
        await World.RotateSecurityStampAsync(await World.ArrangeUserAsync());

        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldNotBeNull();
    }

    [Test]
    public async Task Validate_WhenWrongAudience_ShouldReturnNull()
    {
        var (_, _, pair) = await World.IssueAsync();

        (await World.Tokens.ValidateAsync(pair.AccessToken, "https://other.example.test/mcp")).ShouldBeNull();
        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource + "/")).ShouldBeNull();
    }

    [Test]
    public async Task Validate_WhenTwoUsersHoldTokens_ShouldReturnEachTheirOwn()
    {
        var (firstUser, _, first) = await World.IssueAsync();
        var (secondUser, _, second) = await World.IssueAsync();

        (await World.Tokens.ValidateAsync(first.AccessToken, Resource))!.UserId.ShouldBe(firstUser);
        (await World.Tokens.ValidateAsync(second.AccessToken, Resource))!.UserId.ShouldBe(secondUser);
    }

    [Test]
    public async Task Validate_WhenStampChangedBetweenConsentAndIssue_ShouldRefuseIssue()
    {
        var (userId, _, authorizationId) = await World.AuthorizeAsync();
        await World.RotateSecurityStampAsync(userId);

        (await Should.ThrowAsync<ApiException>(() => World.Issuer.IssueAsync(authorizationId, Resource)))
            .Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Validate_WhenAccessTokenRevokedByToken_ShouldReturnNull()
    {
        var (_, _, pair) = await World.IssueAsync();

        await World.RevokeAsync(pair.AccessToken);

        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldBeNull();
    }
}
