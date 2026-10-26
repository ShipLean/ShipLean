using ModelContextProtocol.Client;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.TestSupport.Mcp;

// An MCP client for a test: connects to {BaseUrl}/mcp with a bearer token. It starts nothing itself. Start the host with
// user null (no test sign-in, so only the bearer counts), get a token from host.IssueAgentToken, and pass it to
// ConnectAsync.
public static class McpTestClient
{
    public static Task<McpClient> ConnectAsync(ShipLeanHost host, string bearer, CancellationToken cancellationToken = default)
        => ConnectAsync(new Uri(host.BaseUrl + "/mcp"), bearer, cancellationToken);

    public static async Task<McpClient> ConnectAsync(Uri endpoint, string bearer, CancellationToken cancellationToken = default)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = endpoint,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = "Bearer " + bearer },
        });
        return await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
    }
}
