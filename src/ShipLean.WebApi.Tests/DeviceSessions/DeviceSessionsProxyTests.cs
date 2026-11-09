using System.Net;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.DeviceSessions;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.DeviceSessions;

namespace ShipLean.WebApi.Tests.DeviceSessions;

// Identity-proxy mode: the server refuses the app (D8). No code is made, and a device bearer is never accepted.
[TestFixture]
public class DeviceSessionsProxyTests : ProxyApiFixture
{
    [Test]
    public async Task Codes_WhenProxyMode_ShouldRefuse()
    {
        using var browser = ProxyClient().WithIdentity("joel@shiplean.local", "Joel Dickson");

        using var response = await browser.Api.PostJsonAsync("api/auth/device-sessions/codes", new CreateSignInCodeRequest("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", "Joel's iPad"));

        var error = await response.ReadApiErrorAsync(HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Error.ShouldBe(DeviceSignInController.ProxyRefusal);
    }

    [Test]
    public async Task Me_WhenProxyModeAndDeviceBearer_ShouldNotSignIn()
    {
        using var phone = await DeviceBearerClient.CreateAsync(Host, SeedIds.Users.Joel);

        using var response = await phone.Client.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
