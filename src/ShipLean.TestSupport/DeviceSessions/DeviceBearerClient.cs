using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Identity;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.TestSupport.Hosting;
using ShipLean.Contracts;

namespace ShipLean.TestSupport.DeviceSessions;

// The phone app stand-in for API tests: a device session made through IDeviceSessions (code then exchange, under the
// user's current security stamp), and an HttpClient that sends its secret as `Authorization: Bearer` with the client
// header the core sends. Start the host without test sign-in (UseTestSignIn false): the test scheme signs in any
// request that has no cookie, bearer or not.
public static class DeviceBearerClient
{
    public static async Task<DeviceBearerSession> CreateAsync(ShipLeanHost host, Guid userId, string deviceName = "Test phone", DateTimeOffset? now = null)
    {
        var at = now ?? TimeProvider.System.GetUtcNow();
        var stamp = (await host.Services.GetRequiredService<UserSessions>().GetAsync(userId, CancellationToken.None))?.SecurityStamp
                    ?? throw new ArgumentException("A device session needs a user with a row.", nameof(userId));
        var sessions = host.Services.GetRequiredService<IDeviceSessions>();
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        var code = await sessions.CreateCodeAsync(userId, stamp, DeviceSecret.ChallengeFor(verifier), deviceName, at);
        var signIn = await sessions.ExchangeAsync(code, verifier, at) ?? throw new InvalidOperationException("The exchange was refused.");
        return new DeviceBearerSession(For(host, signIn.Secret), signIn);
    }

    // A client that sends this secret, live or not.
    public static HttpClient For(ShipLeanHost host, string secret)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { BaseAddress = new Uri(host.BaseUrl) };
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {secret}");
        client.DefaultRequestHeaders.Add(ClientHeader.Name, "device-tests");
        return client;
    }
}
