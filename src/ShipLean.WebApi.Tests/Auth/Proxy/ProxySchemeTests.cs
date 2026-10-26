using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// The scheme seam (K2) in proxy mode, through the real schemes (no test sign-in). The token header is Authorization, so
// on /mcp an agent's bearer could be mistaken for a proxy token if /mcp were not skipped.
[TestFixture]
public class ProxySchemeTests
{
    private ClonedDatabase _database = null!;
    private ShipLeanHost _host = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        _database = await ShipLeanDatabase.CloneSeedAsync();
        _host = ShipLeanHost.Start(_database.ConnectionString, user: null, settings: new Dictionary<string, string?>
        {
            [ProxySignInSettings.TokenHeaderKey] = "Authorization",
            [ProxySignInSettings.IssuerKey] = "https://idp.shiplean.test",
            [ProxySignInSettings.JwksUrlKey] = "http://127.0.0.1:9/jwks",
        });
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(_host.BaseUrl) };
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        _client?.Dispose();
        if (_host != null)
        {
            await _host.DisposeAsync();
        }
        if (_database != null)
        {
            await _database.DisposeAsync();
        }
    }

    [Test]
    public async Task Scheme_WhenProxyModeOnMcp_ShouldStillTakeAgentBearer()
    {
        var token = await _host.IssueAgentToken(TestUser.Mei);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).ShouldContain("\"tools\"");
    }

    [Test]
    public async Task Scheme_WhenProxyModeWithNeitherCookieNorIdentity_ShouldAnswerUnauthorized()
    {
        using var api = await _client.GetAsync("/api/me");
        using var page = await _client.GetAsync("/");

        (await ApiClient.ReadApiErrorAsync(api, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
        page.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        page.Headers.Location!.PathAndQuery.ShouldBe("/login?returnUrl=%2F");
    }
}
