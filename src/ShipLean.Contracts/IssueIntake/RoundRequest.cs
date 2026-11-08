using ShipLean.Contracts.Events;

namespace ShipLean.Contracts.IssueIntake;

// The answer to IIssueProposalSessions.RequestRoundAsync. Requested: a round job was enqueued and the round counted, or a pending
// round already covers the request. Held: nothing was enqueued; Reason says why (the rate limit, the round cap, or another holder's
// reason). Changed is set when the request wrote a ShipLean comment on the item (a hold notice): the caller owns the transaction,
// so it publishes Changed through IProjectEvents after it commits, and drops it on a rollback.
public sealed record RoundRequest(RoundRequestOutcome Outcome, string? Reason = null, CommentsChanged? Changed = null)
{
    public static RoundRequest Requested { get; } = new(RoundRequestOutcome.Requested);

    public static RoundRequest Held(string reason, CommentsChanged? changed = null) => new(RoundRequestOutcome.Held, reason, changed);
}

public enum RoundRequestOutcome
{
    Requested,
    Held,
}
