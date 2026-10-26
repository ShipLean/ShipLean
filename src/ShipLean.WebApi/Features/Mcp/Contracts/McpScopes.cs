namespace ShipLean.WebApi.Features.Mcp.Contracts;

// The scope names an agent token carries (and the consent screen lists).
public static class McpScopes
{
    public const string Read = "shiplean:read";
    public const string Write = "shiplean:write";
    public const string Plan = "shiplean:plan";
}
