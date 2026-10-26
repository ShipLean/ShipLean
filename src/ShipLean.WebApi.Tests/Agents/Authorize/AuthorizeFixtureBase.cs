using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Agents;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Agents.Authorize;

// A browser stand-in over /oauth/authorize: no redirects followed, a real cookie for the signed-in tests. The seed's base_url
// is https://shiplean.larkspur.dev, so that is the issuer and its /mcp the resource.
public abstract class AuthorizeFixtureBase : ApiFixture
{
    protected const string Issuer = "https://shiplean.larkspur.dev";
    protected const string Resource = Issuer + "/mcp";
    protected const string Redirect = "http://127.0.0.1:8123/callback";

    protected static string Challenge => AgentWorld.Challenge;

    protected async Task<string> ArrangeClientAsync(string name = "Authorize Bot", string redirect = Redirect)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IOAuthClients>().RegisterAsync(new OAuthClientRegistration(name, [redirect]))).ClientId;
    }

    protected async Task<HttpClient> SignedInBrowserAsync(bool mustChangePassword = false)
    {
        var builder = new AppUserBuilder();
        if (mustChangePassword)
        {
            builder.MustChangePassword();
        }
        await using (var connection = await OpenDatabaseAsync())
        {
            await builder.InsertAsync(connection);
        }
        return await AuthClient.SignedInAsync(Host, builder.Email, AppUserBuilder.ArrangedPassword);
    }

    protected HttpClient SignedOutBrowser()
    {
        var browser = AuthClient.Create(Host);
        browser.DefaultRequestHeaders.Add(TestAuthentication.UserHeader, TestAuthentication.AnonymousValue);
        return browser;
    }

    // The query of a valid request, with overrides ("name" => null removes a parameter).
    protected static string AuthorizeUrl(string clientId, params (string Name, string? Value)[] overrides)
    {
        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = Redirect,
            ["code_challenge"] = Challenge,
            ["code_challenge_method"] = "S256",
            ["resource"] = Resource,
            ["scope"] = "shiplean:read shiplean:write",
            ["state"] = "st-1",
        };
        foreach (var (name, value) in overrides)
        {
            query[name] = value;
        }
        return QueryHelpers.AddQueryString("/oauth/authorize", query.Where(pair => pair.Value is not null));
    }

    protected static Uri Location(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        return new Uri(new Uri("http://shiplean.test"), response.Headers.Location!.OriginalString);
    }

    protected static Dictionary<string, StringValues> QueryOf(Uri uri) => QueryHelpers.ParseQuery(uri.Query);

    protected static async Task<ConsentResult> DecideAsync(HttpClient browser, Guid requestId, ConsentDecision decision)
    {
        using var response = await browser.PostAsJsonAsync($"/api/agents/consent/{requestId}", decision, AuthClient.Json);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ConsentResult>(AuthClient.Json))!;
    }

    protected static Guid RequestIdOf(Uri location)
    {
        location.AbsolutePath.ShouldBe("/authorize");
        return Guid.Parse(QueryOf(location)["request"].ToString());
    }
}
