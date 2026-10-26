using Shouldly;
using static ShipLean.TestSupport.Contracts.Agents.AgentWorld;
using ShipLean.Contracts.Errors;

namespace ShipLean.TestSupport.Contracts.Agents;

// Behaviour every IAgentTokenIssuer has: access 1 hour, refresh 90 days sliding, rotation, reuse detection.
[TestFixture]
public abstract class AgentTokenIssuerContract
{
    protected abstract AgentWorld World { get; }

    [Test]
    public async Task Issue_ShouldGiveDistinctLongTokensValidForOneHour()
    {
        var (_, _, pair) = await World.IssueAsync("shiplean:read", "shiplean:plan");

        pair.AccessToken.Length.ShouldBeGreaterThanOrEqualTo(43);
        pair.RefreshToken.Length.ShouldBeGreaterThanOrEqualTo(43);
        pair.AccessToken.ShouldNotBe(pair.RefreshToken);
        pair.AccessToken.ShouldMatch("^[A-Za-z0-9_-]+$");
        pair.RefreshToken.ShouldMatch("^[A-Za-z0-9_-]+$");
        pair.AccessExpiresAt.ShouldBe(World.Now + TimeSpan.FromHours(1), TimeSpan.FromSeconds(30));
        pair.Scopes.ShouldBe(["shiplean:read", "shiplean:plan"], ignoreOrder: true);
    }

    [Test]
    public async Task Issue_WhenCalledTwice_ShouldGiveDifferentTokens()
    {
        var (_, authorizationId, first) = await World.IssueAsync();

        var second = await World.Issuer.IssueAsync(authorizationId, Resource);

        second.AccessToken.ShouldNotBe(first.AccessToken);
        second.RefreshToken.ShouldNotBe(first.RefreshToken);
    }

    [Test]
    public async Task Issue_WhenAuthorizationRevoked_ShouldBeNotFound()
    {
        var (userId, authorizationId, _) = await World.IssueAsync();
        await World.Authorizations.RevokeAsync(userId, authorizationId);

        (await Should.ThrowAsync<ApiException>(() => World.Issuer.IssueAsync(authorizationId, Resource))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Issue_WhenUserDisabled_ShouldThrowNotFound()
    {
        var (userId, _, authorizationId) = await World.AuthorizeAsync();
        await World.DisableUserAsync(userId);

        (await Should.ThrowAsync<ApiException>(() => World.Issuer.IssueAsync(authorizationId, Resource))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Issue_WhenStampChanged_ShouldThrowNotFound()
    {
        var (userId, _, authorizationId) = await World.AuthorizeAsync();
        await World.RotateSecurityStampAsync(userId);

        (await Should.ThrowAsync<ApiException>(() => World.Issuer.IssueAsync(authorizationId, Resource))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Issue_WhenClientArchived_ShouldThrowNotFound()
    {
        var (_, clientId, authorizationId) = await World.AuthorizeAsync();
        await World.ArchiveClientAsync(clientId);

        (await Should.ThrowAsync<ApiException>(() => World.Issuer.IssueAsync(authorizationId, Resource))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Issue_WhenAuthorizationUnknown_ShouldBeNotFound()
    {
        (await Should.ThrowAsync<ApiException>(() => World.Issuer.IssueAsync(Guid.NewGuid(), Resource))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Refresh_ShouldRotateAndGiveWorkingPair()
    {
        var (userId, _, first) = await World.IssueAsync();

        var second = await World.RefreshAsync(first.RefreshToken, Resource);

        second.ShouldNotBeNull();
        second.AccessToken.ShouldNotBe(first.AccessToken);
        second.RefreshToken.ShouldNotBe(first.RefreshToken);
        (await World.Tokens.ValidateAsync(second.AccessToken, Resource))!.UserId.ShouldBe(userId);
        (await World.RefreshAsync(second.RefreshToken, Resource)).ShouldNotBeNull();
    }

    [Test]
    public async Task Refresh_WhenReused_ShouldRevokeFamily()
    {
        var (_, _, first) = await World.IssueAsync();
        var second = await World.RefreshAsync(first.RefreshToken, Resource);

        (await World.RefreshAsync(first.RefreshToken, Resource)).ShouldBeNull();

        (await World.RefreshAsync(second!.RefreshToken, Resource)).ShouldBeNull();
        (await World.Tokens.ValidateAsync(second.AccessToken, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Refresh_WhenUnknown_ShouldReturnNull()
    {
        (await World.RefreshAsync("not-a-token", Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Refresh_WhenAccessTokenPresented_ShouldReturnNull()
    {
        var (_, _, pair) = await World.IssueAsync();

        (await World.RefreshAsync(pair.AccessToken, Resource)).ShouldBeNull();
        (await World.RefreshAsync(pair.RefreshToken, Resource)).ShouldNotBeNull();
    }

    [Test]
    public async Task Refresh_WhenWrongResource_ShouldReturnNull()
    {
        var (_, _, pair) = await World.IssueAsync();

        (await World.RefreshAsync(pair.RefreshToken, "https://other.example.test/mcp")).ShouldBeNull();
    }

    [Test]
    public async Task Refresh_WhenExpired_ShouldReturnNull()
    {
        var (_, _, pair) = await World.IssueAsync();
        World.Advance(TimeSpan.FromDays(90) + TimeSpan.FromSeconds(1));

        (await World.RefreshAsync(pair.RefreshToken, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Refresh_WhenUsedRegularly_ShouldSlide()
    {
        var (_, _, pair) = await World.IssueAsync();
        for (var round = 0; round < 3; round++)
        {
            World.Advance(TimeSpan.FromDays(60));
            pair = (await World.RefreshAsync(pair.RefreshToken, Resource))!;
            pair.ShouldNotBeNull();
        }

        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldNotBeNull();
    }

    [Test]
    public async Task Refresh_WhenAuthorizationRevoked_ShouldReturnNull()
    {
        var (userId, authorizationId, pair) = await World.IssueAsync();
        await World.Authorizations.RevokeAsync(userId, authorizationId);

        (await World.RefreshAsync(pair.RefreshToken, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Refresh_WhenUserDisabled_ShouldReturnNull()
    {
        var (userId, _, pair) = await World.IssueAsync();
        await World.DisableUserAsync(userId);

        (await World.RefreshAsync(pair.RefreshToken, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Refresh_WhenSecurityStampChanged_ShouldReturnNull()
    {
        var (userId, _, pair) = await World.IssueAsync();
        await World.RotateSecurityStampAsync(userId);

        (await World.RefreshAsync(pair.RefreshToken, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Refresh_WhenOtherClient_ShouldReturnNull()
    {
        var (_, _, pair) = await World.IssueAsync();
        var otherClient = await World.RegisterClientAsync("Other Agent");

        (await World.Issuer.RefreshAsync(pair.RefreshToken, otherClient, Resource)).ShouldBeNull();

        (await World.RefreshAsync(pair.RefreshToken)).ShouldNotBeNull();
    }

    [Test]
    public async Task Refresh_WhenReused_ShouldAlsoKillTheFirstPairsAccessToken()
    {
        var (_, _, first) = await World.IssueAsync();
        (await World.Tokens.ValidateAsync(first.AccessToken, Resource)).ShouldNotBeNull();
        (await World.RefreshAsync(first.RefreshToken)).ShouldNotBeNull();

        (await World.RefreshAsync(first.RefreshToken)).ShouldBeNull();

        (await World.Tokens.ValidateAsync(first.AccessToken, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Refresh_WhenConcurrent_ShouldRotateAtMostOnce()
    {
        var (_, _, pair) = await World.IssueAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => World.RefreshAsync(pair.RefreshToken))));

        results.Count(r => r is not null).ShouldBe(1);
    }

    [Test]
    public async Task Refresh_WhenStampChangedBetweenConsentAndIssue_ShouldRefuseIssue()
    {
        var (userId, _, authorizationId) = await World.AuthorizeAsync("shiplean:read", "shiplean:write");
        await World.RotateSecurityStampAsync(userId);

        var refused = await Should.ThrowAsync<ApiException>(() => World.Issuer.IssueAsync(authorizationId, Resource));

        refused.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Revoke_WhenAccessToken_ShouldKillOnlyThatToken()
    {
        var (_, _, pair) = await World.IssueAsync();

        await World.RevokeAsync(pair.AccessToken);

        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldBeNull();
        (await World.RefreshAsync(pair.RefreshToken)).ShouldNotBeNull();
    }

    [Test]
    public async Task Revoke_WhenRefreshToken_ShouldKillTheWholeFamily()
    {
        var (_, _, first) = await World.IssueAsync();
        var second = await World.RefreshAsync(first.RefreshToken);

        await World.RevokeAsync(second!.RefreshToken);

        (await World.Tokens.ValidateAsync(second.AccessToken, Resource)).ShouldBeNull();
        (await World.RefreshAsync(second.RefreshToken)).ShouldBeNull();
    }

    [Test]
    public async Task Revoke_WhenRefreshToken_ShouldLeaveOtherFamiliesAlone()
    {
        var (_, authorizationId, first) = await World.IssueAsync();
        var other = await World.Issuer.IssueAsync(authorizationId, Resource);

        await World.RevokeAsync(first.RefreshToken);

        (await World.Tokens.ValidateAsync(other.AccessToken, Resource)).ShouldNotBeNull();
    }

    [Test]
    public async Task Revoke_WhenOtherClientsToken_ShouldDoNothing()
    {
        var (_, _, pair) = await World.IssueAsync();
        var otherClient = await World.RegisterClientAsync("Other Agent");

        await World.Issuer.RevokeAsync(pair.AccessToken, otherClient);
        await World.Issuer.RevokeAsync(pair.RefreshToken, otherClient);

        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldNotBeNull();
        (await World.RefreshAsync(pair.RefreshToken)).ShouldNotBeNull();
    }

    [TestCase("not-a-token")]
    [TestCase("")]
    public async Task Revoke_WhenUnknown_ShouldDoNothing(string token)
    {
        var (_, _, pair) = await World.IssueAsync();

        await World.RevokeAsync(token);

        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldNotBeNull();
    }
}
