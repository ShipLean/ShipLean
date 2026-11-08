using System.Data.Common;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Rounds;

// TODO: temporary; the real proposal sessions (advisory lock, rate limit, round cap, a `round` job) replace this. Until then
// every request is held, so nothing starts a round.
public sealed class NotBuiltProposalSessions : IIssueProposalSessions
{
    public const string NotBuilt = "notBuilt";

    public Task<RoundRequest> RequestRoundAsync(Guid issueId, bool force, DbTransaction transaction, CancellationToken cancellationToken = default)
        => Task.FromResult(RoundRequest.Held(NotBuilt));
}
