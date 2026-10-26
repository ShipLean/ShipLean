using System.Net;
using System.Net.Http.Json;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// The name comes from the identity provider, the colour is the user's own (K10).
[TestFixture]
public class ProxyProfileTests : ApiFixture
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => ProxyApiFixture.LiteralTokenSettings;

    protected override TestUser SignedInAs => TestUser.Priya;

    [Test]
    public async Task UpdateProfile_WhenProxyModeAndNameChanged_ShouldBeForbidden()
    {
        using var response = await Api.PutJsonAsync("api/me/profile", new { name = "A Different Name", avatarColour = "#16a089" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Forbidden)).Code.ShouldBe(ApiErrorCodes.Forbidden);
        var me = await MeAsync();
        me.Name.ShouldNotBe("A Different Name");
    }

    [Test]
    public async Task UpdateProfile_WhenProxyModeAndOnlyColourChanged_ShouldSave()
    {
        var before = await MeAsync();
        var colour = before.AvatarColour == "#16a089" ? "#7b4fc9" : "#16a089";

        using var response = await Api.PutJsonAsync("api/me/profile", new { name = $" {before.Name} ", avatarColour = colour });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var after = await MeAsync();
        after.AvatarColour.ShouldBe(colour);
        after.Name.ShouldBe(before.Name);
    }

    private async Task<MeDto> MeAsync()
    {
        using var response = await Api.GetAsync("api/me");
        return (await response.Content.ReadFromJsonAsync<MeDto>(AuthClient.Json))!;
    }
}
