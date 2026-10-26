using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Agents.Authorize;

// The client, the redirect URI and the person: what /oauth/authorize settles before any protocol error.
[TestFixture]
public sealed class AuthorizeRedirectAndSignInTests : AuthorizeFixtureBase
{
    [TestCase("http://evil.example/callback")]
    [TestCase("http://127.0.0.1:8123/other")]
    [TestCase(null)]
    public async Task Authorize_WhenRedirectNotRegistered_ShouldNotRedirect(string? redirect)
    {
        var clientId = await ArrangeClientAsync();
        using var browser = await SignedInBrowserAsync();

        using var response = await browser.GetAsync(AuthorizeUrl(clientId, ("redirect_uri", redirect)));

        response.Headers.Location.ShouldBeNull();
        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Authorize_WhenRedirectNotRegisteredAndSignedOut_ShouldSendToSignIn()
    {
        var clientId = await ArrangeClientAsync();
        using var browser = SignedOutBrowser();

        using var response = await browser.GetAsync(AuthorizeUrl(clientId, ("redirect_uri", "http://evil.example/cb")));

        Location(response).AbsolutePath.ShouldBe("/login");
    }

    [TestCase("no-such-client")]
    [TestCase(null)]
    public async Task Authorize_WhenClientUnknown_ShouldNotRedirect(string? clientId)
    {
        using var browser = await SignedInBrowserAsync();

        using var response = await browser.GetAsync(AuthorizeUrl("x", ("client_id", clientId)));

        response.Headers.Location.ShouldBeNull();
        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Authorize_WhenRedirectIsLoopbackOnAnotherPort_ShouldAcceptIt()
    {
        var clientId = await ArrangeClientAsync();
        using var browser = await SignedInBrowserAsync();

        using var response = await browser.GetAsync(AuthorizeUrl(clientId, ("redirect_uri", "http://127.0.0.1:54321/callback")));

        RequestIdOf(Location(response)).ShouldNotBe(Guid.Empty);
    }

    [Test]
    public async Task Authorize_WhenSignedOut_ShouldSendToSignInAndBack()
    {
        var clientId = await ArrangeClientAsync();
        var url = AuthorizeUrl(clientId);
        using var browser = SignedOutBrowser();

        using var response = await browser.GetAsync(url);

        var location = Location(response);
        location.AbsolutePath.ShouldBe("/login");
        QueryOf(location).ContainsKey("state").ShouldBeFalse();
        QueryOf(location)["returnUrl"].ToString().ShouldBe(url);
    }

    [Test]
    public async Task Authorize_WhenPasswordChangeRequired_ShouldSendToChangePassword()
    {
        var clientId = await ArrangeClientAsync();
        var url = AuthorizeUrl(clientId);
        using var browser = await SignedInBrowserAsync(mustChangePassword: true);

        using var response = await browser.GetAsync(url);

        var location = Location(response);
        location.AbsolutePath.ShouldBe("/login");
        QueryOf(location)["state"].ToString().ShouldBe("change");
        QueryOf(location)["returnUrl"].ToString().ShouldBe(url);
    }

    [Test]
    public async Task Authorize_WhenSignedOut_ShouldSendToSignInWithoutLookingTheClientUp()
    {
        using var browser = SignedOutBrowser();

        using var response = await browser.GetAsync(AuthorizeUrl("https://nowhere.example/client.json", ("redirect_uri", "http://evil.example/cb")));

        Location(response).AbsolutePath.ShouldBe("/login");
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM oauth_client WHERE client_id = 'https://nowhere.example/client.json'")).ShouldBe(0);
    }

    [TestCase("response_type", "token")]
    [TestCase("code_challenge_method", "plain")]
    [TestCase("resource", "https://elsewhere.example/mcp")]
    [TestCase("scope", "shiplean:admin")]
    public async Task Authorize_WhenHttpsRedirectAndProtocolError_ShouldShowThePageAndNeverRedirect(string name, string value)
    {
        var clientId = await ArrangeClientAsync(redirect: "https://app.example/cb");
        using var browser = await SignedInBrowserAsync();

        using var response = await browser.GetAsync(AuthorizeUrl(clientId, ("redirect_uri", "https://app.example/cb"), (name, value)));

        response.Headers.Location.ShouldBeNull();
        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Authorize_WhenHttpsRedirectAndValid_ShouldReachConsent()
    {
        var clientId = await ArrangeClientAsync(redirect: "https://app.example/cb");
        using var browser = await SignedInBrowserAsync();

        using var response = await browser.GetAsync(AuthorizeUrl(clientId, ("redirect_uri", "https://app.example/cb")));

        RequestIdOf(Location(response)).ShouldNotBe(Guid.Empty);
    }

    [TestCase("response_type", "token")]
    [TestCase("code_challenge_method", "plain")]
    [TestCase("scope", "shiplean:admin")]
    public async Task Authorize_WhenCustomSchemeRedirectAndProtocolError_ShouldNotRedirect(string name, string value)
    {
        const string cursor = "cursor://anysphere.cursor-retrieval/oauth/callback";
        var clientId = await ArrangeClientAsync(redirect: cursor);
        using var browser = await SignedInBrowserAsync();

        using var response = await browser.GetAsync(AuthorizeUrl(clientId, ("redirect_uri", cursor), (name, value)));

        response.Headers.Location.ShouldBeNull();
        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Authorize_WhenCustomSchemeRedirectDiffers_ShouldNotRedirect()
    {
        var clientId = await ArrangeClientAsync(redirect: "cursor://anysphere.cursor-retrieval/oauth/callback");
        using var browser = await SignedInBrowserAsync();

        using var response = await browser.GetAsync(AuthorizeUrl(clientId, ("redirect_uri", "cursor://anysphere.cursor-retrieval/other")));

        response.Headers.Location.ShouldBeNull();
        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
