using System.Net;
using ModelContextProtocol.Client;
using ShipLean.TestSupport.Contracts.Agents;
using ShipLean.TestSupport.Mcp;

namespace ShipLean.WebApi.Tests.Agents.Tokens;

[TestFixture]
public class TokenRefreshTests : TokenFixture
{
    [Test]
    public async Task Token_WhenRefreshed_ShouldRotateThePair()
    {
        var arranged = await ArrangeAuthorizationAsync();
        var first = await RedeemAsync(arranged);

        var (response, body) = await TokenAsync(RefreshGrant(arranged.ClientId, first.GetProperty("refresh_token").GetString()!));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("refresh_token").GetString().ShouldNotBe(first.GetProperty("refresh_token").GetString());
        (await ValidateAsync(body.GetProperty("access_token").GetString()!)).ShouldNotBeNull();
    }

    [Test]
    public async Task Token_WhenRefreshReused_ShouldRevokeFamily()
    {
        var arranged = await ArrangeAuthorizationAsync();
        var first = await RedeemAsync(arranged);
        var spent = first.GetProperty("refresh_token").GetString()!;
        var (rotated, rotatedBody) = await TokenAsync(RefreshGrant(arranged.ClientId, spent));
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (reuse, reuseBody) = await TokenAsync(RefreshGrant(arranged.ClientId, spent));

        RefusalOf(reuse, reuseBody).ShouldBe("invalid_grant");
        (await ValidateAsync(rotatedBody.GetProperty("access_token").GetString()!)).ShouldBeNull();
        var (next, nextBody) = await TokenAsync(RefreshGrant(arranged.ClientId, rotatedBody.GetProperty("refresh_token").GetString()!));
        RefusalOf(next, nextBody).ShouldBe("invalid_grant");
    }

    [Test]
    public async Task Token_WhenRefreshResourceWrong_ShouldRefuseAsInvalidTarget()
    {
        var arranged = await ArrangeAuthorizationAsync();
        var first = await RedeemAsync(arranged);

        var (response, body) = await TokenAsync(RefreshGrant(arranged.ClientId, first.GetProperty("refresh_token").GetString()!, "https://elsewhere.example/mcp"));

        RefusalOf(response, body).ShouldBe("invalid_target");
    }

    [Test]
    public async Task Token_WhenUserDisabled_ShouldRefuse()
    {
        var viaCode = await ArrangeAuthorizationAsync();
        var code = await IssueCodeAsync(viaCode.AuthorizationId);
        await ExecuteAsync("UPDATE app_user SET disabled_at = now() WHERE id = @id", new { id = viaCode.UserId });
        var (response, body) = await TokenAsync(CodeGrant(viaCode.ClientId, code));
        RefusalOf(response, body).ShouldBe("invalid_grant");

        var viaRefresh = await ArrangeAuthorizationAsync();
        var pair = await RedeemAsync(viaRefresh);
        await ExecuteAsync("UPDATE app_user SET disabled_at = now() WHERE id = @id", new { id = viaRefresh.UserId });
        var (refresh, refreshBody) = await TokenAsync(RefreshGrant(viaRefresh.ClientId, pair.GetProperty("refresh_token").GetString()!));
        RefusalOf(refresh, refreshBody).ShouldBe("invalid_grant");
    }

    [Test]
    public async Task Token_WhenPasswordChanged_ShouldRefuse()
    {
        var viaCode = await ArrangeAuthorizationAsync();
        var code = await IssueCodeAsync(viaCode.AuthorizationId);
        await ChangePasswordAsync(viaCode.UserId, "changed-password-1");
        var (response, body) = await TokenAsync(CodeGrant(viaCode.ClientId, code));
        RefusalOf(response, body).ShouldBe("invalid_grant");

        var viaRefresh = await ArrangeAuthorizationAsync();
        var pair = await RedeemAsync(viaRefresh);
        await ChangePasswordAsync(viaRefresh.UserId, "changed-password-2");
        var (refresh, refreshBody) = await TokenAsync(RefreshGrant(viaRefresh.ClientId, pair.GetProperty("refresh_token").GetString()!));
        RefusalOf(refresh, refreshBody).ShouldBe("invalid_grant");
    }
}
