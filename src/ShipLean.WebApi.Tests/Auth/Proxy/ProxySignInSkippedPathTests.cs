using Dapper;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// The machine OAuth endpoints keep their own scheme: proxy identity headers there sign nobody in and create nobody.
[TestFixture]
public class ProxySignInSkippedPathTests : ProxyApiFixture
{
    [Test]
    public async Task SignIn_WhenOnOAuthTokenWithIdentity_ShouldNotReadIdentity()
    {
        var email = $"proxy-{Guid.NewGuid():N}@shiplean.test";
        using var client = ProxyClient().WithIdentity(email, "Agent Smith");
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "authorization_code", ["code"] = "nope" });

        using var response = await client.Http.PostAsync("/oauth/token", form);

        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        await using var db = await OpenDatabaseAsync();
        (await db.QuerySingleAsync<int>("SELECT count(*)::int FROM app_user WHERE lower(email) = lower(@email)", new { email })).ShouldBe(0);
    }

    [Test]
    public async Task SignIn_WhenPutOnUploadUrlWithIdentityAndNoClientHeader_ShouldNotReadIdentityOrRefuseTheWrite()
    {
        var email = $"proxy-{Guid.NewGuid():N}@shiplean.test";
        using var client = ProxyClient().WithIdentity(email, "Agent Smith");
        using var content = new ByteArrayContent([1, 2, 3]);

        using var response = await client.Http.PutAsync("/api/uploads/nope", content);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        await using var db = await OpenDatabaseAsync();
        (await db.QuerySingleAsync<int>("SELECT count(*)::int FROM app_user WHERE lower(email) = lower(@email)", new { email })).ShouldBe(0);
    }
}
