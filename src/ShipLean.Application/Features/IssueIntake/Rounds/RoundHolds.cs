using System.Data.Common;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Rounds;

// Holds an issue for a maintainer (D11, D16), in the caller's transaction: a compare-and-set of the phase to waitingForMaintainer
// and one holding comment per issue and reason (its dedupe key), so a hold repeated for the same reason posts nothing more.
// An accepted or closed conversation is left as it is, and nothing is posted while the issue is closed on GitHub.
public sealed class RoundHolds(IGitHubIssueStore issues, IGitHubOutbox outbox)
{
    public const string Comment = "A maintainer will pick this up.";

    public const string RateLimit = "rateLimit";

    public const string RoundCap = "roundCap";

    public const string NoRunAs = "noRunAs";

    private static readonly IssuePhase[] Holdable = [IssuePhase.Proposing, IssuePhase.AwaitingReply, IssuePhase.AlreadyShipped];

    public static string DedupeKey(Guid issueId, string reason) => $"held:{issueId}:{reason}";

    public async Task<RoundRequest> HoldAsync(GitHubIssueRecord issue, string reason, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var moved = await issues.SetPhaseAsync(issue.Id, Holdable, IssuePhase.WaitingForMaintainer, transaction, cancellationToken);
        if ((moved || issue.Phase == IssuePhase.WaitingForMaintainer) && issue.OpenOnGitHub)
        {
            await outbox.EnqueueAsync(OutboxEntry.Comment(issue.Id, DedupeKey(issue.Id, reason), Comment), transaction, cancellationToken);
        }

        return RoundRequest.Held(reason);
    }
}
