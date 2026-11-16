using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.Assembly;

// Which logs belong to an interaction span: the log carries its span id, or carries none and has the interaction's prompt id
// and a time inside the span.
public static class LogJoin
{
    private const string UserPromptSuffix = "user_prompt";
    private const string PromptAttribute = "prompt";
    private const string ToolUseIdAttribute = "tool_use_id";

    public static bool JoinsInteraction(LogRecordData log, SpanRecord interaction, string? promptId)
    {
        if (log.SpanId is not null)
        {
            return log.SpanId == interaction.SpanId;
        }
        return promptId is not null && log.PromptId == promptId && log.Time >= interaction.Start && log.Time <= interaction.End;
    }

    // The turn's prompt id: the interaction span's own, else the one its logs carry (Claude Code's spans have none, its logs do).
    public static string? PromptIdOf(IReadOnlyList<LogRecordData> logs, SpanRecord interaction)
        => interaction.Attributes.GetValueOrDefault(InspectionFieldNames.Defaults.Prompt)
            ?? logs.Where(log => log.SpanId == interaction.SpanId).Select(log => log.PromptId).FirstOrDefault(id => id is not null);

    // A log belongs to a row when it carries the row's span id, or the same tool_use_id as the tool's span: Claude Code's tool_result and
    // tool_decision records point at the interaction or the permission-wait span, not at the tool, but share the tool call id.
    public static bool BelongsToRow(LogRecordData log, SpanRecord span)
        => log.SpanId == span.SpanId
            || (span.Attributes.GetValueOrDefault(ToolUseIdAttribute) is { } toolUse && log.Attributes.GetValueOrDefault(ToolUseIdAttribute) == toolUse);

    public static bool IsUserPrompt(LogRecordData log) => log.EventName is { } name && name.EndsWith(UserPromptSuffix, StringComparison.Ordinal);

    // The prompt text of a turn: the user_prompt record joined to the interaction.
    public static string? PromptText(IReadOnlyList<LogRecordData> logs, SpanRecord interaction, string? promptId)
        => logs.Where(log => IsUserPrompt(log) && JoinsInteraction(log, interaction, promptId))
            .Select(log => log.Attributes.GetValueOrDefault(PromptAttribute))
            .FirstOrDefault(text => text is not null);
}
