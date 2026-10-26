using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Realtime;

public static class HubSignIn
{
    // The sign-in cookie as a request header, from a real login (so the cookie scheme authenticates it).
    public static async Task<string> CookieAsync(ShipLeanHost host, string email, string password)
    {
        using var client = AuthClient.Create(host);
        using var response = await client.LoginAsync(email, password);
        response.EnsureSuccessStatusCode();
        return response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
    }
}
