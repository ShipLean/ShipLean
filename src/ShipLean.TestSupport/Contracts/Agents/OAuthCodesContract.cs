using Shouldly;
using static ShipLean.TestSupport.Contracts.Agents.AgentWorld;
using ShipLean.Contracts.Errors;

namespace ShipLean.TestSupport.Contracts.Agents;

// Behaviour every IOAuthCodes has: S256 only, single use, 60 seconds, every argument must match.
[TestFixture]
public abstract class OAuthCodesContract
{
    protected abstract AgentWorld World { get; }

    [Test]
    public async Task Redeem_WhenEverythingMatches_ShouldReturnAuthorizationId()
    {
        var (_, clientId, authorizationId) = await World.AuthorizeAsync();
        var code = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);

        (await World.Codes.RedeemAsync(code, Verifier, LoopbackRedirect, clientId, Resource)).ShouldBe(authorizationId);
    }

    [Test]
    public async Task Issue_WhenTwice_ShouldGiveDifferentCodes()
    {
        var (_, _, authorizationId) = await World.AuthorizeAsync();

        var first = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);
        var second = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);

        first.Length.ShouldBeGreaterThanOrEqualTo(43);
        second.ShouldNotBe(first);
    }

    [Test]
    public async Task Issue_WhenChallengeIsNotS256_ShouldBeValidation()
    {
        var (_, _, authorizationId) = await World.AuthorizeAsync();

        // A "plain" challenge is the verifier itself; the only accepted shape is a 43-character S256 digest.
        (await Should.ThrowAsync<ApiException>(() => World.Codes.IssueAsync(authorizationId, "plain-challenge", LoopbackRedirect, Resource))).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Issue_WhenAuthorizationUnknown_ShouldBeNotFound()
    {
        (await Should.ThrowAsync<ApiException>(() => World.Codes.IssueAsync(Guid.NewGuid(), Challenge, LoopbackRedirect, Resource))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Redeem_WhenUnknown_ShouldReturnNull()
    {
        var (_, clientId, _) = await World.AuthorizeAsync();

        (await World.Codes.RedeemAsync("not-a-code", Verifier, LoopbackRedirect, clientId, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenVerifierWrong_ShouldReturnNull()
    {
        var (_, clientId, authorizationId) = await World.AuthorizeAsync();
        var code = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);

        (await World.Codes.RedeemAsync(code, Verifier.Replace('d', 'e'), LoopbackRedirect, clientId, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenVerifierIsTheChallenge_ShouldReturnNull()
    {
        var (_, clientId, authorizationId) = await World.AuthorizeAsync();
        var code = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);

        (await World.Codes.RedeemAsync(code, Challenge, LoopbackRedirect, clientId, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenVerifierWrong_ShouldSpendTheCode()
    {
        var (_, clientId, authorizationId) = await World.AuthorizeAsync();
        var code = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);

        (await World.Codes.RedeemAsync(code, Verifier.Replace('d', 'e'), LoopbackRedirect, clientId, Resource)).ShouldBeNull();

        (await World.Codes.RedeemAsync(code, Verifier, LoopbackRedirect, clientId, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenRedirectDiffers_ShouldReturnNull()
    {
        var (_, clientId, authorizationId) = await World.AuthorizeAsync();
        var code = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);

        (await World.Codes.RedeemAsync(code, Verifier, "http://127.0.0.1:8123/other", clientId, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenClientDiffers_ShouldReturnNull()
    {
        var (_, _, authorizationId) = await World.AuthorizeAsync();
        var otherClient = await World.RegisterClientAsync("Another Agent");
        var code = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);

        (await World.Codes.RedeemAsync(code, Verifier, LoopbackRedirect, otherClient, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenResourceDiffers_ShouldReturnNull()
    {
        var (_, clientId, authorizationId) = await World.AuthorizeAsync();
        var code = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);

        (await World.Codes.RedeemAsync(code, Verifier, LoopbackRedirect, clientId, "https://other.example.test/mcp")).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenJustBeforeExpiry_ShouldReturnAuthorizationId()
    {
        var (_, clientId, authorizationId) = await World.AuthorizeAsync();
        var code = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);
        World.Advance(TimeSpan.FromSeconds(59));

        (await World.Codes.RedeemAsync(code, Verifier, LoopbackRedirect, clientId, Resource)).ShouldBe(authorizationId);
    }

    [Test]
    public async Task Redeem_WhenExpired_ShouldReturnNull()
    {
        var (_, clientId, authorizationId) = await World.AuthorizeAsync();
        var code = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);
        World.Advance(TimeSpan.FromSeconds(61));

        (await World.Codes.RedeemAsync(code, Verifier, LoopbackRedirect, clientId, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenReused_ShouldRevokeAuthorization()
    {
        var (userId, clientId, authorizationId) = await World.AuthorizeAsync();
        var pair = await World.Issuer.IssueAsync(authorizationId, Resource);
        var code = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);
        (await World.Codes.RedeemAsync(code, Verifier, LoopbackRedirect, clientId, Resource)).ShouldBe(authorizationId);

        (await World.Codes.RedeemAsync(code, Verifier, LoopbackRedirect, clientId, Resource)).ShouldBeNull();

        (await World.Authorizations.ListAsync(userId)).ShouldBeEmpty();
        (await World.Tokens.ValidateAsync(pair.AccessToken, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenAuthorizationRevoked_ShouldReturnNull()
    {
        var (userId, clientId, authorizationId) = await World.AuthorizeAsync();
        var code = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);
        await World.Authorizations.RevokeAsync(userId, authorizationId);

        (await World.Codes.RedeemAsync(code, Verifier, LoopbackRedirect, clientId, Resource)).ShouldBeNull();
    }

    [Test]
    public async Task Redeem_WhenConcurrent_ShouldSucceedAtMostOnce()
    {
        var (_, clientId, authorizationId) = await World.AuthorizeAsync();
        var code = await World.Codes.IssueAsync(authorizationId, Challenge, LoopbackRedirect, Resource);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => World.Codes.RedeemAsync(code, Verifier, LoopbackRedirect, clientId, Resource))));

        results.Count(r => r is not null).ShouldBe(1);
    }
}
