namespace ShipLean.WebApi.Features.Mcp.Transport;

// Resolves McpGatewaySettings at startup, and when the header is on says so. The header name is not a secret; the value never is.
public sealed class McpGatewayStartupLog(McpGatewaySettings settings, ILogger<McpGatewayStartupLog> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (settings.EmailHeader is { } header)
        {
            logger.LogInformation(
                "MCP gateway: /mcp trusts the {Header} header as the caller. A proxy in front must set it and strip any copy the client sent.",
                header);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
