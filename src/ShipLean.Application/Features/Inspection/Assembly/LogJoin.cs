using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.Assembly;

// Which logs belong to an interaction span: the log carries its span id, or carries none and has the interaction's prompt id
// and a time inside the span.
public static class LogJoin
{
    private const string UserPromptSuffix = "user_prompt";
    private const string PromptAttribute = "prompt";

    public static bool JoinsInteraction(LogRecordData log, SpanRecord interaction, string? promptId)
    {
        if (log.SpanId is not null)
        {
            return log.SpanId == interaction.SpanId;
        }
        return promptId is not null && log.PromptId == promptId && log.Time >= interaction.Start && log.Time <= interaction.End;
    }

    public static bool IsUserPrompt(LogRecordData log) => log.EventName is { } name && name.EndsWith(UserPromptSuffix, StringComparison.Ordinal);

    // The prompt text of a turn: the user_prompt record joined to the interaction.
    public static string? PromptText(IReadOnlyList<LogRecordData> logs, SpanRecord interaction, string? promptId)
        => logs.Where(log => IsUserPrompt(log) && JoinsInteraction(log, interaction, promptId))
            .Select(log => log.Attributes.GetValueOrDefault(PromptAttribute))
            .FirstOrDefault(text => text is not null);
}
