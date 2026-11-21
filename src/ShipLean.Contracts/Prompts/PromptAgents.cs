namespace ShipLean.Contracts.Prompts;

// The agents a prompt can address, recognised the way DropMCP does it. Self-reported: it picks sections and labels, never access.
public static class PromptAgents
{
    public const string ClaudeCode = "claude-code";
    public const string OpenAiCli = "codex-cli";
    public const string Cursor = "cursor";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = [ClaudeCode, OpenAiCli, Cursor, Other];

    public static string Label(string id) => id switch
    {
        ClaudeCode => "Claude Code",
        OpenAiCli => "Codex CLI",
        Cursor => "Cursor",
        _ => "Another agent",
    };

    // Copied from DropMCP (github.com/agoda-com/dropmcp @ ca92342a2b62c8e60b679e34f8b196cf954781f2, src/dropmcp/telemetry.py):
    // _normalize_client_name for Normalize, _resolve_client_fields for Of. ShipLean adds the OAuth client name as the last signal.
    // _meta.agent is not used: ShipLean reads no per-call _meta.
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Other;
        }
        var slug = Slug(raw);
        if (slug.Contains("co" + "dex", StringComparison.Ordinal))
        {
            return OpenAiCli;
        }
        if (slug.Contains("cursor", StringComparison.Ordinal))
        {
            return Cursor;
        }
        return slug.Contains("claude", StringComparison.Ordinal) && slug.Contains("code", StringComparison.Ordinal) ? ClaudeCode : Other;
    }

    public static ConnectedAgent Of(AgentSignals signals)
    {
        if (Present(signals.McpClientName) is { } initialize)
        {
            return new ConnectedAgent(Normalize(initialize), AgentSignalSource.Initialize, initialize);
        }
        if (Present(signals.Originator) is { } originator)
        {
            return new ConnectedAgent(Normalize(originator), AgentSignalSource.Originator, originator);
        }
        if (Present(signals.UserAgent) is { } userAgent)
        {
            var slash = userAgent.IndexOf('/', StringComparison.Ordinal);
            var product = slash < 0 ? userAgent : userAgent[..slash].Trim();
            return new ConnectedAgent(Normalize(userAgent), AgentSignalSource.UserAgent, product);
        }
        return Present(signals.OAuthClientName) is { } client
            ? new ConnectedAgent(Normalize(client), AgentSignalSource.OAuthClient, client)
            : new ConnectedAgent(Other, AgentSignalSource.None, null);
    }

    private static string? Present(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Lowercase, each run of other characters becomes one '-', trimmed of '-'.
    private static string Slug(string raw)
    {
        var slug = new System.Text.StringBuilder();
        foreach (var c in raw.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }
        return slug.ToString().Trim('-');
    }
}
