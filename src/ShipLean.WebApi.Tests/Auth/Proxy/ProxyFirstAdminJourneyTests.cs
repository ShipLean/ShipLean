using System.Text.Json.Nodes;
using ShipLean.Application.Identity;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// SHIP-63 J1: on a database with no users, SHIPLEAN_ADMIN_EMAIL alone makes its owner the instance admin at the first
// token sign-in; a different first user does not become one.
[TestFixture]
public class ProxyFirstAdminJourneyTests : ProxyApiFixture
{
    private const string AdminEmail = "journey.admin@example.test";

    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override Task<ClonedDatabase> CloneDatabaseAsync() => ShipLeanDatabase.CloneEmptyAsync();

    protected override IReadOnlyDictionary<string, string?> ProxySettings
        => new Dictionary<string, string?>(Provider!.Settings()) { [AdminBootstrap.EmailKey] = AdminEmail };

    [Test]
    public async Task FirstAdmin_WhenAdminEmailAloneAndNoAdmin_ShouldPromoteAtFirstSignIn()
    {
        using var other = ProxyClient().WithToken("X-Test-Proxy-Token", Provider!.Mint("first.user@example.test", "First User"));
        var otherMe = await other.GetJsonAsync<JsonNode>("/api/me");
        otherMe["isInstanceAdmin"]!.GetValue<bool>().ShouldBeFalse();

        using var client = ProxyClient().WithToken("X-Test-Proxy-Token", Provider.Mint(AdminEmail, "First Admin"));

        var me = await client.GetJsonAsync<JsonNode>("/api/me");

        me["email"]!.GetValue<string>().ShouldBe(AdminEmail);
        me["isInstanceAdmin"]!.GetValue<bool>().ShouldBeTrue();
    }
}
