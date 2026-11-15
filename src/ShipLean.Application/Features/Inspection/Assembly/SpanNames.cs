namespace ShipLean.Application.Features.Inspection.Assembly;

// Claude Code's span names (its monitoring docs) that the assembler gives a meaning.
internal static class SpanNames
{
    public const string Interaction = "claude_code.interaction";
    public const string LlmRequest = "claude_code.llm_request";
    public const string Subagent = "claude_code.subagent";
    public const string BlockedOnUser = "claude_code.tool.blocked_on_user";
    public const string Execution = "claude_code.tool.execution";
}
