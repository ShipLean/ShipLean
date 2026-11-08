using System.Data.Common;

namespace ShipLean.Contracts.IssueIntake;

// Asks for a proposal round on an issue (C4, D11, D16). In the caller's transaction it takes the project's advisory lock,
// checks the rate limit and the round cap (both skipped when force is true, as for a maintainer's /shiplean), increments the
// issue's rounds and enqueues a `round` job. Held when the rate or the cap stops it: then no job is enqueued, and the issue moves
// to waitingForMaintainer with its hold notices (written only when that move changes the phase; RoundRequest.Changed then says
// which item's comments to publish after commit). A pending round job of the issue covers the request (Requested, nothing more
// enqueued); when that job's session has already started, the issue's rerun_requested flag is set instead, for the inbox to
// take (IGitHubIssueStore.TakeRerunAsync) once that round's proposal is posted. An unforced request for an issue that is
// proposing with a round behind it (rounds > 0) and no pending round job is a round in flight too: it sets rerun_requested and
// returns Requested, and never enqueues a second round.
// The exceptions write nothing at all, no phase change and no notices: Held("intakeOff"), because the issue's project no
// longer takes its repository and posting on its behalf would be wrong, and Held("issueClosed"), forced or not, because the
// issue is closed on GitHub and nothing posts while it is closed (D13).
public interface IIssueProposalSessions
{
    Task<RoundRequest> RequestRoundAsync(Guid issueId, bool force, DbTransaction transaction, CancellationToken cancellationToken = default);

    // Takes the project's round lock (a transaction-scoped advisory lock, re-entrant) in the caller's transaction. Lock order for
    // every issue writer: this lock first, then rows. RequestRoundAsync and the inbox take it before any row; a caller that writes
    // rows (a mirrored reply) before it asks for a round takes it first, so it can't deadlock with them.
    Task LockProjectAsync(Guid projectId, DbTransaction transaction, CancellationToken cancellationToken = default);
}
