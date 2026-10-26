using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// The base of the /mcp tests: no test sign-in (only a bearer counts), the probe tools registered, and a raw client for
// the status, headers and body the typed MCP client hides. One scenario per class, like every ApiFixture.
public abstract class McpFixture : ApiFixture
{
    protected const string AllScopes = "shiplean:read shiplean:write shiplean:plan";

    protected static readonly string[] ReadOnly = ["shiplean:read"];
    protected static readonly string[] ReadWrite = ["shiplean:read", "shiplean:write"];
    protected static readonly string[] ReadWritePlan = ["shiplean:read", "shiplean:write", "shiplean:plan"];

    protected override TestUser SignedInAs => TestUser.Anonymous;

    [SetUp]
    public void ResetProbeLog() => Log.Reset();

    protected ProbeLog Log => Host.Services.GetRequiredService<ProbeLog>();

    // False for a scenario that must see only the tools the app registers itself (the sweep).
    protected virtual bool WithProbeTools => true;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<ProbeLog>();
        if (WithProbeTools)
        {
            services.AddMcpServer().WithTools<ProbeTools>().WithTools<TypeScopedProbeTools>().WithTools<UnscopedProbeTools>();
        }
    };

    protected Task<string> TokenAsync(TestUser user, string[] scopes, string clientName = "Test Agent")
        => Host.IssueAgentToken(user, scopes.ToHashSet(), clientName);

    protected Task<McpReply> CallToolAsync(string? bearer, string tool, string arguments = "{}", Action<HttpRequestMessage>? configure = null)
        => PostAsync(bearer, $$$"""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"{{{tool}}}","arguments":{{{arguments}}}}}""", configure);

    protected async Task<McpReply> PostAsync(string? bearer, string json, Action<HttpRequestMessage>? configure = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return await SendAsync(request, bearer, configure);
    }

    protected async Task<McpReply> SendAsync(HttpRequestMessage request, string? bearer, Action<HttpRequestMessage>? configure = null)
    {
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
        configure?.Invoke(request);
        using var response = await Client.SendAsync(request);
        return new McpReply(response.StatusCode, response.Headers, await response.Content.ReadAsStringAsync());
    }
}
