using System.Data.Common;

namespace ShipLean.Contracts.IssueIntake;

// Asks for a proposal round on an issue (C4, D11, D16). In the caller's transaction it takes the project's advisory lock,
// checks the rate limit and the round cap (both skipped when force is true, as for a maintainer's /shiplean), increments the
// issue's rounds and enqueues a `round` job. Held when the rate or the cap stops it: then no job is enqueued, and the issue moves
// to waitingForMaintainer with its hold notices (written only when that move changes the phase; RoundRequest.Changed then says
// which item's comments to publish after commit). A pending round job of the issue covers the request (Requested, nothing more
// enqueued); when that job's session has already started, the issue's rerun_requested flag is set instead, for the inbox to
// take (IGitHubIssueStore.TakeRerunAsync) once that round's proposal is posted.
// The one exception: Held("intakeOff") writes nothing at all, no phase change and no notices, because the issue's project no
// longer takes its repository and posting on its behalf would be wrong.
public interface IIssueProposalSessions
{
    Task<RoundRequest> RequestRoundAsync(Guid issueId, bool force, DbTransaction transaction, CancellationToken cancellationToken = default);
}
