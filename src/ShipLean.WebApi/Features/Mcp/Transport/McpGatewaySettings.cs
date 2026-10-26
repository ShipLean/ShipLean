using Microsoft.Extensions.Configuration;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// SHIPLEAN_MCP_GATEWAY_EMAIL_HEADER. Unset (or blank), /mcp ignores an email header and accepts only an agent bearer.
// Set, that header names the caller. The variable does not make the header trustworthy: a proxy in front must write it
// and strip any copy the client sent, and no client may reach /mcp directly.
public sealed record McpGatewaySettings(string? EmailHeader)
{
    public const string EmailHeaderKey = "SHIPLEAN_MCP_GATEWAY_EMAIL_HEADER";

    public static McpGatewaySettings Off { get; } = new((string?)null);

    public static McpGatewaySettings Parse(IConfiguration configuration)
    {
        var header = configuration[EmailHeaderKey]?.Trim();
        return string.IsNullOrEmpty(header) ? Off : new McpGatewaySettings(header);
    }
}
