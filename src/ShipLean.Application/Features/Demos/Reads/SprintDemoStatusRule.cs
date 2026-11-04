using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Reads;

// K6's status rules, first match wins. Pure over the facts and the clock's now.
internal static class SprintDemoStatusRule
{
    public const string CloseFirst = "Close the sprint to make its demo.";

    public const string LinkExpired = "The upload link expired before a video came back.";

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

        if (facts.LatestSessionState is "failed" or "canceled" or "stale" && facts.LatestSessionEndedAt >= requested)
        {
            return (SprintDemoStatus.Failed, $"The agent session ended as {facts.LatestSessionState} before a video came back.");
        }

        return (SprintDemoStatus.Requested, null);
    }
}
