using System.Net;
using ModelContextProtocol.Client;
using ShipLean.TestSupport.Contracts.Agents;
using ShipLean.TestSupport.Mcp;

namespace ShipLean.WebApi.Tests.Agents.Tokens;

[TestFixture]
public class RevokeTests : TokenFixture
{
    [Test]
    public async Task Revoke_WhenUnknownToken_ShouldAnswer200()
    {
        var arranged = await ArrangeAuthorizationAsync();

        var (response, _) = await PostAsync("/oauth/revoke", new() { ["token"] = "never-issued", ["client_id"] = arranged.ClientId });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Revoke_WhenRefresh_ShouldEndFamily()
    {
        var arranged = await ArrangeAuthorizationAsync();
        var pair = await RedeemAsync(arranged);

        var (response, _) = await PostAsync("/oauth/revoke", new() { ["token"] = pair.GetProperty("refresh_token").GetString()!, ["client_id"] = arranged.ClientId });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ValidateAsync(pair.GetProperty("access_token").GetString()!)).ShouldBeNull();
        var (refresh, body) = await TokenAsync(RefreshGrant(arranged.ClientId, pair.GetProperty("refresh_token").GetString()!));
        RefusalOf(refresh, body).ShouldBe("invalid_grant");
    }

    [Test]
    public async Task Revoke_WhenAccessToken_ShouldEndOnlyThatToken()
    {
        var arranged = await ArrangeAuthorizationAsync();
        var pair = await RedeemAsync(arranged);

        await PostAsync("/oauth/revoke", new() { ["token"] = pair.GetProperty("access_token").GetString()!, ["client_id"] = arranged.ClientId });

        (await ValidateAsync(pair.GetProperty("access_token").GetString()!)).ShouldBeNull();
        var (refresh, _) = await TokenAsync(RefreshGrant(arranged.ClientId, pair.GetProperty("refresh_token").GetString()!));
        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Revoke_WhenOtherClient_ShouldChangeNothing()
    {
        var arranged = await ArrangeAuthorizationAsync();
        var other = await ArrangeAuthorizationAsync();
        var pair = await RedeemAsync(arranged);

        var (response, _) = await PostAsync("/oauth/revoke", new() { ["token"] = pair.GetProperty("refresh_token").GetString()!, ["client_id"] = other.ClientId });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ValidateAsync(pair.GetProperty("access_token").GetString()!)).ShouldNotBeNull();
    }
}
