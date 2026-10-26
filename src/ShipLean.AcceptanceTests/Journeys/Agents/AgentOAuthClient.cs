using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Agents;
using ShipLean.AcceptanceTests.Pages.Auth;

namespace ShipLean.AcceptanceTests.Journeys.Agents;

// An agent speaking OAuth to the app the way a real one does: it registers itself, sends the person's browser to
// authorize, catches the code on its loopback redirect and exchanges it for a token. Everything the person does
// happens in the browser context handed to AuthorizeAsync.
public sealed class AgentOAuthClient(string baseUrl, string clientName, int callbackPort) : IDisposable
{
    public const string Read = "shiplean:read";
    public const string Write = "shiplean:write";
    public const string Plan = "shiplean:plan";

    private readonly HttpClient http = new() { BaseAddress = new Uri(baseUrl) };

    public string RedirectUri => $"http://127.0.0.1:{callbackPort}/callback";

    public string ClientId { get; private set; } = string.Empty;

    // The protected resource the app names for /mcp (the seed's base URL decides it, not the test host's address).
    public string Resource { get; private set; } = string.Empty;

    // Dynamic client registration, then the resource from the protected-resource metadata.
    public async Task RegisterAsync()
    {
        using var registered = await http.PostAsJsonAsync("/oauth/register", new { client_name = clientName, redirect_uris = new[] { RedirectUri } });
        registered.EnsureSuccessStatusCode();
        ClientId = (await registered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("client_id").GetString()!;

        var metadata = await http.GetFromJsonAsync<JsonElement>("/.well-known/oauth-protected-resource/mcp");
        Resource = metadata.GetProperty("resource").GetString()!;
    }

    // Opens authorize in a new page of the context, takes the person through sign-in (when asked) and consent, and
    // returns the token endpoint's answer for the code the redirect carries. `consent` runs on the consent page before Allow.
    public async Task<JsonElement> AuthorizeAsync(IBrowserContext context, string[] scopes, string? signInEmail = null, string? signInPassword = null, Func<ConsentScreen, Task>? consent = null)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Guid.NewGuid().ToString("N");
        var page = await context.NewPageAsync();
        try
        {
            await page.RouteAsync($"http://127.0.0.1:{callbackPort}/**", route =>
                route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "text/html", Body = "<html></html>" }));
            await page.GotoAsync(AuthorizeUrl(scopes, challenge, state));

            if (signInEmail is not null)
            {
                var login = new LoginScreen(page);
                await login.SignInForm.WaitForAsync();
                await login.SignInAsync(signInEmail, signInPassword!);
            }

            var screen = new ConsentScreen(page);
            await screen.Allow.WaitForAsync();
            if (consent is not null)
            {
                await consent(screen);
            }
            await screen.Allow.ClickAsync();
            await page.WaitForURLAsync(new Regex("^" + Regex.Escape(RedirectUri) + @"\?"));

            var query = HttpUtility.ParseQueryString(new Uri(page.Url).Query);
            query.Get("state").ShouldBe(state);
            var code = query.Get("code");
            code.ShouldNotBeNullOrEmpty();
            return await ExchangeAsync(code, verifier);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private string AuthorizeUrl(string[] scopes, string challenge, string state) =>
        "/oauth/authorize" + Links.Query(
            ("response_type", "code"),
            ("client_id", ClientId),
            ("redirect_uri", RedirectUri),
            ("code_challenge", challenge),
            ("code_challenge_method", "S256"),
            ("resource", Resource),
            ("scope", string.Join(' ', scopes)),
            ("state", state));

    private async Task<JsonElement> ExchangeAsync(string code, string verifier)
    {
        using var response = await http.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = RedirectUri,
            ["resource"] = Resource,
        }));
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    // A raw JSON-RPC tools/call: the typed client hides the status and headers a step-up answer carries.
    public async Task<AgentMcpReply> CallRawAsync(string bearer, string tool, string arguments = "{}")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent($$$"""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"{{{tool}}}","arguments":{{{arguments}}}}}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var response = await http.SendAsync(request);
        return new AgentMcpReply(response.StatusCode, response.Headers.WwwAuthenticate.ToString());
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => http.Dispose();
}
