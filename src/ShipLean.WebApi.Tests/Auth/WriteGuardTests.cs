using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth;

// A write that carries the session cookie must send JSON or the client header; one without the cookie is not checked.
[TestFixture]
public class WriteGuardTests : ApiFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    [Test]
    public async Task Write_WhenCookieAndNoHeaderAndNoJson_ShouldBe403()
    {
        using var client = await SignedInWithoutHeaderAsync();

        using var response = await client.PostAsync("/api/auth/sign-out-others", null);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
    }

    [Test]
    public async Task Write_WhenCookieAndFormBody_ShouldBe403()
    {
        using var client = await SignedInWithoutHeaderAsync();

        using var response = await client.PostAsync("/api/auth/sign-out-others", new FormUrlEncodedContent([new("a", "b")]));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Write_WhenCookieAndClientHeader_ShouldPass()
    {
        using var client = await SignedInWithoutHeaderAsync();
        client.DefaultRequestHeaders.Add("X-ShipLean-Client", "tab-1");

        using var response = await client.PostAsync("/api/auth/logout", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Write_WhenCookieAndJsonBody_ShouldPass()
    {
        using var client = await SignedInWithoutHeaderAsync();

        using var response = await client.PostAsync("/api/auth/logout", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<HttpClient> SignedInWithoutHeaderAsync()
    {
        using var login = AuthClient.Create(Host);
        var client = AuthClient.Create(Host, sendClientHeader: false);
        using var response = await login.LoginAsync("sam@shiplean.local", SeedIds.Passwords.Sam);
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
        return client;
    }
}
