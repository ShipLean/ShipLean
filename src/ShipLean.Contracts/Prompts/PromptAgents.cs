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

    public static string Normalize(string? raw)
    {
        // TODO: the slug rules (OpenAI CLI name, cursor, claude and code) copied from DropMCP
        return Other;
    }

    public static ConnectedAgent Of(AgentSignals signals)
    {
        // TODO: the first non-blank signal in DropMCP's order, normalised
        return new ConnectedAgent(Other, AgentSignalSource.None, null);
    }
}
