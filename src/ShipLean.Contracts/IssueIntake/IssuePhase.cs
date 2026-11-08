namespace ShipLean.Contracts.IssueIntake;

// Where a taken issue's conversation is. Proposing: a round is writing a proposal. AwaitingReply: a proposal is posted and
// waits for LGTM or a reply. AlreadyShipped: the posted answer says it already shipped. WaitingForMaintainer: held (round
// cap, rate limit, a failed write). Accepting: an LGTM was taken and its accept job is running; only that job moves the phase
// on, and it counts as not accepted yet. Accepted: an LGTM was acted on to the end. Closed: the conversation is over.
public enum IssuePhase
{
    Proposing,
    AwaitingReply,
    AlreadyShipped,
    WaitingForMaintainer,
    Accepting,
    Accepted,
    Closed,
}
