using System.Net;
using System.Text.Json;
using Dapper;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.Auth;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// Proxy sign-in's ordinary path: first sight creates the user and issues the cookie, the cookie then carries the
// session, and last_sign_in_at moves only when a cookie is issued.
[TestFixture]
public class ProxySignInTests : ProxyApiFixture
{
    [Test]
    public async Task SignIn_WhenIdentityAndNoCookie_ShouldCreateUserAndSetCookie()
    {
        var email = NewEmail();
        using var client = ProxyClient().WithIdentity(email, "Ada Lovelace");

        using var response = await client.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await MeAsync(response)).Email.ShouldBe(email);
        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith(AuthServices.CookieName + "=", StringComparison.Ordinal) && !c.StartsWith(AuthServices.CookieName + "=;", StringComparison.Ordinal));
        (await NamesAsync(email)).ShouldBe(["Ada Lovelace"]);
    }

    [Test]
    public async Task SignIn_WhenSameIdentityWithCookie_ShouldKeepTheUser()
    {
        var email = NewEmail();
        using var client = ProxyClient().WithIdentity(email, "Grace Hopper");
        var first = await client.GetJsonAsync<Me>("/api/me");

        using var again = await client.GetAsync("/api/me");

        (await MeAsync(again)).Id.ShouldBe(first.Id);
        again.Headers.Contains("Set-Cookie").ShouldBeFalse();
        (await NamesAsync(email)).Count.ShouldBe(1);
    }

    [Test]
    public async Task SignIn_WhenCookieAndNoIdentity_ShouldKeepServing()
    {
        var email = NewEmail();
        using var client = ProxyClient().WithIdentity(email);
        var first = await client.GetJsonAsync<Me>("/api/me");
        client.WithoutIdentity();

        var me = await client.GetJsonAsync<Me>("/api/me");

        me.Id.ShouldBe(first.Id);
    }

    [Test]
    public async Task SignIn_WhenCookieIssued_ShouldRecordLastSignIn()
    {
        var email = NewEmail();
        using var client = ProxyClient().WithIdentity(email);

        var me = await client.GetJsonAsync<Me>("/api/me");
        var afterFirst = await LastSignInAsync(me.Id);
        await using (var db = await OpenDatabaseAsync())
        {
            await db.ExecuteAsync("UPDATE app_user SET last_sign_in_at = '2001-01-01T00:00:00Z' WHERE id = @id", new { id = me.Id });
        }
        await client.GetJsonAsync<Me>("/api/me");

        afterFirst.ShouldNotBeNull();
        (await LastSignInAsync(me.Id)).ShouldBe(new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    private static string NewEmail() => $"proxy-{Guid.NewGuid():N}@shiplean.test";

    private static async Task<Me> MeAsync(HttpResponseMessage response)
        => JsonSerializer.Deserialize<Me>(await response.Content.ReadAsStringAsync(), JsonSerializerOptions.Web)!;

    private async Task<List<string>> NamesAsync(string email)
    {
        await using var db = await OpenDatabaseAsync();
        return (await db.QueryAsync<string>("SELECT name FROM app_user WHERE lower(email) = lower(@email)", new { email })).ToList();
    }

    private async Task<DateTime?> LastSignInAsync(Guid id)
    {
        await using var db = await OpenDatabaseAsync();
        return await db.QuerySingleAsync<DateTime?>("SELECT last_sign_in_at FROM app_user WHERE id = @id", new { id });
    }

    // The /api/me fields these tests read (MeDto's enums are strings on the wire).
    private sealed record Me(Guid Id, string Email, string Name, bool HasProjects);
}
