using Dapper;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// The stored name follows the identity provider whenever it sends one, and is left alone when it sends none.
[TestFixture]
public class ProxySignInNameTests : ProxyApiFixture
{
    [Test]
    public async Task SignIn_WhenProviderNameChanges_ShouldUpdateTheName()
    {
        var email = $"proxy-{Guid.NewGuid():N}@shiplean.test";
        using var client = ProxyClient().WithIdentity(email, "Ada Byron");
        await client.GetJsonAsync<Me>("/api/me");
        client.WithIdentity(email, "Ada Lovelace");

        var me = await client.GetJsonAsync<Me>("/api/me");

        me.Name.ShouldBe("Ada Lovelace");
        (await StoredNameAsync(email)).ShouldBe("Ada Lovelace");
    }

    [Test]
    public async Task SignIn_WhenProviderSendsNoName_ShouldKeepTheName()
    {
        var email = $"proxy-{Guid.NewGuid():N}@shiplean.test";
        using (var first = ProxyClient().WithIdentity(email, "Grace Hopper"))
        {
            await first.GetJsonAsync<Me>("/api/me");
        }
        // A new browser, so the request goes through find-or-create rather than the cookie.
        using var client = ProxyClient().WithIdentity(email);

        var me = await client.GetJsonAsync<Me>("/api/me");

        me.Name.ShouldBe("Grace Hopper");
        (await StoredNameAsync(email)).ShouldBe("Grace Hopper");
    }

    private async Task<string> StoredNameAsync(string email)
    {
        await using var db = await OpenDatabaseAsync();
        return await db.QuerySingleAsync<string>("SELECT name FROM app_user WHERE lower(email) = lower(@email)", new { email });
    }

    // The /api/me fields these tests read (MeDto's enums are strings on the wire).
    private sealed record Me(Guid Id, string Email, string Name, bool HasProjects);
}
