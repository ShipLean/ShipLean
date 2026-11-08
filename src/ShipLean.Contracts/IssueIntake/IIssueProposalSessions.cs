using System.Data.Common;

namespace ShipLean.Contracts.IssueIntake;

// Asks for a proposal round on an issue (C4, D11, D16). In the caller's transaction it takes the project's advisory lock,
// checks the rate limit and the round cap (both skipped when force is true, as for a maintainer's /shiplean), increments the
// issue's rounds and enqueues a `round` job. Held when the rate or the cap stops it; then nothing is written.
public interface IIssueProposalSessions
{
    Task<RoundRequest> RequestRoundAsync(Guid issueId, bool force, DbTransaction transaction, CancellationToken cancellationToken = default);
}
