using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Identity;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Hosting.Proxy;
using ShipLean.WebApi.Features.Auth.Google;

namespace ShipLean.WebApi.Tests.Auth.Google;

// An ApiFixture with no test sign-in and Google sign-in on, pointed at a TestIdentityProvider standing in for Google.
// Everything from the handler on is real: state, nonce, code exchange, signature and audience checks, the cookie.
public abstract class GoogleSignInFixture : ApiFixture
{
    protected const string ClientId = "test-client.apps.googleusercontent.com";
    protected const string ClientSecret = "test-client-secret";

    protected TestIdentityProvider Provider { get; private set; } = null!;

    protected sealed override bool UseTestSignIn => false;

    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        [GoogleSignInSettings.ClientIdKey] = ClientId,
        [GoogleSignInSettings.ClientSecretKey] = ClientSecret,
    };

    protected sealed override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.Configure<OpenIdConnectOptions>(GoogleSignInServices.Scheme, options =>
        {
            options.Authority = Provider.DiscoveryIssuer;
            options.RequireHttpsMetadata = false;
        });

    protected override async Task BeforeHostStartAsync()
    {
        await base.BeforeHostStartAsync();
        Provider = await TestIdentityProvider.StartAsync();
    }

    protected override async Task AfterHostStopAsync()
    {
        await Provider.DisposeAsync();
        await base.AfterHostStopAsync();
    }

    protected HttpClient Browser()
        => new(new BrowserCookies()) { BaseAddress = new Uri(Host.BaseUrl) };

    // The browser's trip to Google and back: Google answers the code exchange with a token for this person, carrying
    // this attempt's nonce unless a test says otherwise.
    protected async Task<HttpResponseMessage> SignInAsync(
        HttpClient browser, string subject, string email, string? name = null, bool emailVerified = true, string? returnUrl = "/p/SHIP", string? audience = null, string? nonce = null)
    {
        var query = await StartAsync(browser, returnUrl);
        Provider.NextIdToken = Provider.Mint(email, name, new TokenOptions
        {
            Issuer = Provider.DiscoveryIssuer,
            Audience = audience ?? ClientId,
            ExtraClaims = new Dictionary<string, object> { ["sub"] = subject, ["nonce"] = nonce ?? query["nonce"].ToString(), ["email_verified"] = emailVerified },
        });
        return await browser.GetAsync($"{GoogleSignInServices.CallbackPath}?code=test-code&state={Uri.EscapeDataString(query["state"].ToString())}");
    }

    protected static async Task<Dictionary<string, Microsoft.Extensions.Primitives.StringValues>> StartAsync(HttpClient browser, string? returnUrl = "/p/SHIP")
    {
        using var start = await browser.GetAsync($"/api/auth/google/start?returnUrl={Uri.EscapeDataString(returnUrl ?? string.Empty)}");
        start.StatusCode.ShouldBe(HttpStatusCode.Found);
        return QueryHelpers.ParseQuery(start.Headers.Location!.Query);
    }

    protected static async Task<JsonNode> MeAsync(HttpClient browser)
    {
        using var me = await browser.GetAsync("/api/me");
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await me.Content.ReadFromJsonAsync<JsonNode>())!;
    }

    protected static async Task ShouldHaveNoSessionAsync(HttpClient browser)
    {
        using var me = await browser.GetAsync("/api/me");
        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
