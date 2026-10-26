using System.Net.Http.Json;
using ShipLean.Contracts.Auth;
using ShipLean.TestSupport.Builders;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Agents.Authorize;

[TestFixture]
public sealed class AuthorizeSignInReturnTests : AuthorizeFixtureBase
{
    // The server half of the round trip: the sign-in page's returnUrl, once signed in, is the authorize URL itself, and
    // opening it goes on to the consent page. (The SPA opens it with a full page load: LoginPage, isServerPath.)
    [Test]
    public async Task Authorize_WhenSignedOutThenSignedIn_ShouldComeBackAndReachConsent()
    {
        var clientId = await ArrangeClientAsync();
        var builder = new AppUserBuilder();
        await using (var connection = await OpenDatabaseAsync())
        {
            await builder.InsertAsync(connection);
        }
        using var browser = AuthClient.Create(Host);
        browser.DefaultRequestHeaders.Add(ShipLean.TestSupport.Hosting.TestAuthentication.UserHeader, ShipLean.TestSupport.Hosting.TestAuthentication.AnonymousValue);
        using var bounced = await browser.GetAsync(AuthorizeUrl(clientId));
        var returnUrl = QueryOf(Location(bounced))["returnUrl"].ToString();

        browser.DefaultRequestHeaders.Remove(ShipLean.TestSupport.Hosting.TestAuthentication.UserHeader);
        using var login = await browser.PostAsJsonAsync("/api/auth/login", new LoginRequest(builder.Email, AppUserBuilder.ArrangedPassword));
        login.EnsureSuccessStatusCode();
        using var back = await browser.GetAsync(returnUrl);

        RequestIdOf(Location(back)).ShouldNotBe(Guid.Empty);
    }
}
