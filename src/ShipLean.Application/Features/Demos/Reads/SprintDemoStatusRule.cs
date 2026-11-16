using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Reads;

// K6's status rules, first match wins. Pure over the facts and the clock's now.
internal static class SprintDemoStatusRule
{
    public const string CloseFirst = "Close the sprint to make its demo.";

    public const string LinkExpired = "The upload link expired before a video came back.";

    public const string CompletedWithoutVideo = "The agent session ended without uploading a video.";

    public static (SprintDemoStatus Status, string? Message) Decide(SprintDemoFacts facts, DateTimeOffset now)
    {
        if (facts.RequestedAt is not { } requested)
        {
            return facts.Closed ? (SprintDemoStatus.None, null) : (SprintDemoStatus.Closable, CloseFirst);
        }

        if (!string.IsNullOrWhiteSpace(facts.BlockedMessage))
        {
            return (SprintDemoStatus.Blocked, facts.BlockedMessage);
        }

        if (facts.LiveSession)
        {
            return (SprintDemoStatus.Working, null);
        }

        if (facts.DeliveredAt is { } delivered && delivered >= requested)
        {
            return (SprintDemoStatus.Ready, null);
        }

        if (facts.UploadExpiresAt is { } expires && expires <= now)
        {
            return (SprintDemoStatus.Failed, LinkExpired);
        }

        if (facts.LatestSessionState is { } state and ("completed" or "failed" or "canceled" or "stale") && facts.LatestSessionEndedAt >= requested)
        {
            return (SprintDemoStatus.Failed, SessionEnded(state) + Quote(facts.LatestSessionMessage));
        }

        return (SprintDemoStatus.Requested, null);
    }

    // P5: the session-ended Failed message split into ShipLean's sentence and the agent's words; null when Decide gives anything else.
    public static SprintDemoAgentSaidDto? Said(SprintDemoFacts facts, DateTimeOffset now)
    {
        var (status, message) = Decide(facts, now);
        if (status != SprintDemoStatus.Failed || facts.LatestSessionState is not { } state || SprintDemoAgentMessage.Excerpt(facts.LatestSessionMessage) is not { } excerpt)
        {
            return null;
        }

        var ended = SessionEnded(state);
        return message == ended + Quote(facts.LatestSessionMessage) ? new SprintDemoAgentSaidDto(ended, excerpt) : null;
    }

    // C5 (D8): any terminal state of a session that ended at or after the request with no video since is Failed.
    private static string SessionEnded(string state)
        => state == "completed" ? CompletedWithoutVideo : $"The agent session ended as {state} before a video came back.";

    private static string Quote(string? message)
        => SprintDemoAgentMessage.Excerpt(message) is { } excerpt ? $" It said: \"{excerpt}\"" : "";
}
