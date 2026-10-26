using System.Text.Json.Nodes;
using ShipLean.Application.Identity;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// SHIP-63 J1: on the seed clone Joel is already an instance admin, so SHIPLEAN_ADMIN_EMAIL for a new email promotes nobody.
[TestFixture]
public class ProxyFirstAdminExistsJourneyTests : ProxyApiFixture
{
    private const string NewAdminEmail = "late.admin@example.test";

    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override IReadOnlyDictionary<string, string?> ProxySettings
        => new Dictionary<string, string?>(Provider!.Settings()) { [AdminBootstrap.EmailKey] = NewAdminEmail };

    [Test]
    public async Task FirstAdmin_WhenAnAdminExists_ShouldDoNothing()
    {
        using var client = ProxyClient().WithToken("X-Test-Proxy-Token", Provider!.Mint(NewAdminEmail, "Late Admin"));

        var me = await client.GetJsonAsync<JsonNode>("/api/me");

        me["email"]!.GetValue<string>().ShouldBe(NewAdminEmail);
        me["isInstanceAdmin"]!.GetValue<bool>().ShouldBeFalse();
    }
}
