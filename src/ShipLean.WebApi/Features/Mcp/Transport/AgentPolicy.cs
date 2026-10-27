namespace ShipLean.WebApi.Features.Mcp.Transport;

// The authorization policy and the rate-limit policy on /mcp share Name (Program.cs maps the endpoint with both). Scheme is
// the bearer scheme only that policy uses: the cookie never signs anyone in on /mcp, and the bearer never signs anyone in
// anywhere else.
public static class AgentPolicy
{
    public const string Name = "agent";

    public const string Scheme = "agent-bearer";

    public const string ResourceMetadataScheme = "agent-resource-metadata";

    // Writing tool calls per user per minute (the rate-limit policy), and failed token lookups per client address per minute.
    public const int WritesPerMinutePerUser = 120;

    public const int FailedLookupsPerMinutePerIp = 20;
}
