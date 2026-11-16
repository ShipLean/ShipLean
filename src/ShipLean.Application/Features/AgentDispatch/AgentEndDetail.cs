namespace ShipLean.Application.Features.AgentDispatch;

// Builds agent_session.end_detail (C2) for the writers that set end_reason. The column holds at most 1,000 characters (its CHECK),
// so every value is cut to fit, never across a surrogate pair. The stale detail is built in AgentStaleSweeper's own UPDATE.
internal static class AgentEndDetail
{
    public const int MaxLength = 1000;

    public const string ProposalWithoutIssue = "The item has no GitHub issue to propose on.";

    // deliveryFailed: the scrubbed error plus the try count, e.g. "No answer within 5 seconds. (2 tries)".
    public static string DeliveryFailed(string error, int tries)
    {
        var count = tries == 1 ? " (1 try)" : $" ({tries} tries)";
        return Cut(error, MaxLength - count.Length) + count;
    }

    // agentError: the first 1,000 characters of the error activity's body.
    public static string? AgentError(string? body) => body is null ? null : Cut(body, MaxLength);

    private static string Cut(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        return text[..(char.IsHighSurrogate(text[max - 1]) ? max - 1 : max)];
    }
}
