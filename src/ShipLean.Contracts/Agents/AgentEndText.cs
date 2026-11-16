namespace ShipLean.Contracts.Agents;

// The one sentence that says why a session ended, for the agent panel, the Demo band and MCP (C2). Detail is what the viewer may
// see (AgentSessionVisibility), so a null or blank detail leaves it out and the sentence ends with a full stop instead.
public static class AgentEndText
{
    public static string For(string endReason, string? detail)
    {
        var text = string.IsNullOrWhiteSpace(detail) ? null : detail;
        return endReason switch
        {
            AgentEndReasons.DeliveryFailed => With("The agent connection didn't take the job", text),
            AgentEndReasons.AgentError => With("The agent reported an error", FirstLine(text)),
            AgentEndReasons.Stale => "The agent went quiet for 30 minutes.",
            AgentEndReasons.Canceled => "The agent session was stopped.",
            AgentEndReasons.Completed => "The agent finished.",
            AgentEndReasons.ProposalWithoutIssue => With("The agent session ended", text),
            AgentEndReasons.Unknown => "The agent session ended before ShipLean recorded why.",
            _ => $"The agent session ended ({endReason}).",
        };
    }

    private static string With(string sentence, string? detail) => detail is null ? $"{sentence}." : $"{sentence}: {detail}";

    private static string? FirstLine(string? text)
    {
        var line = text?.Split('\n', 2)[0].Trim();
        return string.IsNullOrEmpty(line) ? null : line;
    }
}
