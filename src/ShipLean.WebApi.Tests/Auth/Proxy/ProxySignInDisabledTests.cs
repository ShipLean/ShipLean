using System.Net;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// A disabled user is refused even with a valid proxy identity, and a proxy sign-in never re-enables them.
[TestFixture]
public class ProxySignInDisabledTests : ProxyApiFixture
{
    [Test]
    public async Task SignIn_WhenUserDisabled_ShouldAnswerSessionEnded()
    {
        var email = $"proxy-{Guid.NewGuid():N}@shiplean.test";
        using var signedIn = ProxyClient().WithIdentity(email);
        var me = await signedIn.GetJsonAsync<Me>("/api/me");
        await using (var db = await OpenDatabaseAsync())
        {
            await db.ExecuteAsync("UPDATE app_user SET disabled_at = now() WHERE id = @id", new { id = me.Id });
        }
        Host.Services.GetRequiredService<UserSessions>().Forget(me.Id);
        using var fresh = ProxyClient().WithIdentity(email);

        using var withCookie = await signedIn.GetAsync("/api/me");
        using var api = await fresh.GetAsync("/api/me");
        using var page = await fresh.GetAsync("/p/PAY?x=1");

        (await ApiClient.ReadApiErrorAsync(withCookie, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
        (await ApiClient.ReadApiErrorAsync(api, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
        page.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        new Uri(new Uri(Host.BaseUrl), page.Headers.Location!).PathAndQuery.ShouldBe("/login?state=disabled&returnUrl=%2Fp%2FPAY%3Fx%3D1");
        await using var check = await OpenDatabaseAsync();
        (await check.QuerySingleAsync<bool>("SELECT disabled_at IS NOT NULL FROM app_user WHERE id = @id", new { id = me.Id })).ShouldBeTrue();
    }

    [Test]
    public async Task SignIn_WhenOtherUsersCookieAndIdentityDisabled_ShouldEndTheCookie()
    {
        using var client = ProxyClient().WithIdentity(NewEmail());
        await client.GetJsonAsync<Me>("/api/me");
        var disabled = await DisabledUserEmailAsync();
        client.WithIdentity(disabled);

        using var refused = await client.GetAsync("/api/me");
        client.WithoutIdentity();
        using var afterwards = await client.GetAsync("/api/me");

        (await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
        refused.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith(AuthServices.CookieName + "=;", StringComparison.Ordinal));
        (await ApiClient.ReadApiErrorAsync(afterwards, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
    }

    [Test]
    public async Task Hub_WhenUserDisabled_ShouldAnswerSessionEnded()
    {
        using var client = ProxyClient().WithIdentity(await DisabledUserEmailAsync());

        using var negotiate = await client.Http.PostAsync($"{ProjectHub.Path}/negotiate?negotiateVersion=1", null);

        (await ApiClient.ReadApiErrorAsync(negotiate, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.SessionEnded);
    }

    private static string NewEmail() => $"proxy-{Guid.NewGuid():N}@shiplean.test";

    // A user created by proxy sign-in, then disabled.
    private async Task<string> DisabledUserEmailAsync()
    {
        var email = NewEmail();
        using var first = ProxyClient().WithIdentity(email);
        var me = await first.GetJsonAsync<Me>("/api/me");
        await using (var db = await OpenDatabaseAsync())
        {
            await db.ExecuteAsync("UPDATE app_user SET disabled_at = now() WHERE id = @id", new { id = me.Id });
        }
        Host.Services.GetRequiredService<UserSessions>().Forget(me.Id);
        return email;
    }

    // The /api/me fields these tests read (MeDto's enums are strings on the wire).
    private sealed record Me(Guid Id, string Email, string Name, bool HasProjects);
}
