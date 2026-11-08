namespace ShipLean.Contracts.IssueIntake;

// The answer to IIssueProposalSessions.RequestRoundAsync. Requested: a round job was enqueued and the round counted.
// Held: nothing was enqueued; Reason says why (the rate limit, the round cap, or another holder's reason).
public sealed record RoundRequest(RoundRequestOutcome Outcome, string? Reason = null)
{
    public static RoundRequest Requested { get; } = new(RoundRequestOutcome.Requested);

    public static RoundRequest Held(string reason) => new(RoundRequestOutcome.Held, reason);
}

public enum RoundRequestOutcome
{
    Requested,
    Held,
}
