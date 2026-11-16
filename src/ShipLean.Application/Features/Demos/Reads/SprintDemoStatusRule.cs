using ShipLean.Contracts.Agents;
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
            return (SprintDemoStatus.Failed, Ended(state, facts.LatestSessionEndReason, facts.LatestSessionEndDetail, facts.LatestSessionMessage));
        }

        return (SprintDemoStatus.Requested, null);
    }

    // P5: the session-ended Failed message split into ShipLean's sentence and the agent's words; null when Decide gives anything else,
    // including a sentence that carries the end detail and so quotes nothing (SHIP-1620 C2). Built from the same pieces as Ended.
    public static SprintDemoAgentSaidDto? Said(SprintDemoFacts facts, DateTimeOffset now)
    {
        var (status, message) = Decide(facts, now);
        if (status != SprintDemoStatus.Failed || facts.LatestSessionState is not { } state || SprintDemoAgentMessage.Excerpt(facts.LatestSessionMessage) is not { } excerpt)
        {
            return null;
        }

        var sentence = EndedSentence(state, facts.LatestSessionEndReason, facts.LatestSessionEndDetail);
        return message == sentence + Quote(facts.LatestSessionMessage) ? new SprintDemoAgentSaidDto(sentence, excerpt) : null;
    }

    // C5 (D8): any terminal state of a session that ended at or after the request with no video since is Failed. With a recorded end
    // reason the band says why it ended (SHIP-1620 C2), except a plain completion, which keeps the demo's own wording. The agent's last
    // words are quoted after it, unless the sentence already carries a detail the viewer may see: an agentError detail is that same message.
    private static string Ended(string state, string? endReason, string? endDetail, string? message)
    {
        var sentence = EndedSentence(state, endReason, endDetail);
        return string.IsNullOrWhiteSpace(endDetail) ? sentence + Quote(message) : sentence;
    }

    private static string EndedSentence(string state, string? endReason, string? endDetail)
        => endReason is null or AgentEndReasons.Completed ? SessionEnded(state) : AgentEndText.For(endReason, endDetail);

    // A session that ended without a recorded reason (a seed or a writer that predates reasons) keeps the state-only wording.
    private static string SessionEnded(string state)
        => state == "completed" ? CompletedWithoutVideo : $"The agent session ended as {state} before a video came back.";

    private static string Quote(string? message)
        => SprintDemoAgentMessage.Excerpt(message) is { } excerpt ? $" It said: \"{excerpt}\"" : "";
}
