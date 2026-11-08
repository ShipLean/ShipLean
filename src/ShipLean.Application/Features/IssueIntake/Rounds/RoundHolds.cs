using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Rounds;

// Holds an issue for a maintainer (D11, D16), in the caller's transaction: a compare-and-set of the phase to waitingForMaintainer
// (from proposing, awaitingReply or alreadyShipped; an issue already waiting stays so), then the hold notices: one public GitHub
// comment through the outbox (not while the issue is closed on GitHub) and one ShipLean comment on the item saying why, by the
// run-as member as the agent "ShipLean". The notices are claimed once per issue, reason and round count in the effect ledger,
// so a replay, or a second request held in the same state, adds nothing. An accepted or closed conversation is left as it is.
public sealed class RoundHolds(IGitHubIssueStore issues, IGitHubOutbox outbox, IIssueEffects effects, IServiceScopeFactory scopes)
{
    public const string Comment = "This issue is waiting for a maintainer. A maintainer can comment `/shiplean` to continue.";

    public const string AgentName = "ShipLean";

    public const string RateLimit = "rateLimit";

    public const string RoundCap = "roundCap";

    private static readonly IssuePhase[] Holdable = [IssuePhase.Proposing, IssuePhase.AwaitingReply, IssuePhase.AlreadyShipped];

    public static string DedupeKey(Guid issueId, string reason, int rounds) => $"hold:{issueId}:{reason}:{rounds}";

    // True when this hold wrote the notices: the caller publishes CommentsChanged for the item after it commits.
    public async Task<bool> HoldAsync(
        GitHubIssueRecord issue, string reason, string explanation, Guid runAsUserId, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var moved = await issues.SetPhaseAsync(issue.Id, Holdable, IssuePhase.WaitingForMaintainer, transaction, cancellationToken);
        var key = DedupeKey(issue.Id, reason, issue.Rounds);
        if ((!moved && issue.Phase != IssuePhase.WaitingForMaintainer) || !await effects.TryClaimAsync(key, issue.Id, null, transaction, cancellationToken))
        {
            return false;
        }

        if (issue.OpenOnGitHub)
        {
            await outbox.EnqueueAsync(OutboxEntry.Comment(issue.Id, key, Comment), transaction, cancellationToken);
        }

        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICommentStore>()
            .AddAsAgentAsync(issue.WorkItemId, new CommentCreate(explanation), AgentName, runAsUserId, transaction, cancellationToken);
        return true;
    }
}
