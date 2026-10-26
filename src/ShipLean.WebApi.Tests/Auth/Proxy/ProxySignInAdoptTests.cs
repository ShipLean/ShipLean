using Dapper;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// An existing user whose email differs only in case is adopted: same id, same projects, no second row.
[TestFixture]
public class ProxySignInAdoptTests : ProxyApiFixture
{
    [Test]
    public async Task SignIn_WhenEmailDiffersInCaseFromSeedUser_ShouldAdoptThem()
    {
        using var client = ProxyClient().WithIdentity(TestUser.Mei.Email!.ToUpperInvariant());

        var me = await client.GetJsonAsync<Me>("/api/me");

        me.Id.ShouldBe(TestUser.Mei.Id!.Value);
        me.HasProjects.ShouldBeTrue();
        await using var db = await OpenDatabaseAsync();
        (await db.QuerySingleAsync<int>("SELECT count(*)::int FROM app_user WHERE lower(email) = lower(@email)", new { email = TestUser.Mei.Email })).ShouldBe(1);
    }

    // The /api/me fields these tests read (MeDto's enums are strings on the wire).
    private sealed record Me(Guid Id, string Email, string Name, bool HasProjects);
}
