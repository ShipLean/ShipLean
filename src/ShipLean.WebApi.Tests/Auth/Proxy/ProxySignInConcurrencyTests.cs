using System.Net;
using Dapper;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// The SPA's first page load fires several API calls at once: a new identity still becomes exactly one user.
[TestFixture]
public class ProxySignInConcurrencyTests : ProxyApiFixture
{
    [Test]
    public async Task SignIn_WhenTenFirstRequestsRace_ShouldCreateOneUser()
    {
        var email = $"proxy-{Guid.NewGuid():N}@shiplean.test";
        using var client = ProxyClient().WithIdentity(email, "Race Condition");

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => client.GetAsync("/api/me")));

        try
        {
            responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
        await using var db = await OpenDatabaseAsync();
        (await db.QuerySingleAsync<int>("SELECT count(*)::int FROM app_user WHERE lower(email) = lower(@email)", new { email })).ShouldBe(1);
    }
}
