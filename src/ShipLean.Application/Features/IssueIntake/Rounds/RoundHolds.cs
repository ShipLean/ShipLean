using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Rounds;

// Holds an issue for a maintainer (D11, D16), in the caller's transaction: a compare-and-set of the phase to waitingForMaintainer
// (from proposing, awaitingReply or alreadyShipped; an issue already waiting stays so), then the hold notices: one public GitHub
// comment through the outbox (not while the issue is closed on GitHub) and one ShipLean comment on the item saying why, by the
// run-as member as the agent "ShipLean". The notices are written only when the compare-and-set changes the phase: a second
// request held while the issue is already waiting adds nothing, and a hold that recurs after a maintainer's /shiplean resumed the
// issue writes new notices. An accepted or closed conversation is left as it is; so is a closed and reopened issue that is
// still waiting (a maintainer can /shiplean).
public sealed class RoundHolds(IGitHubIssueStore issues, IGitHubOutbox outbox, IServiceScopeFactory scopes)
{
    public const string Comment = "This issue is waiting for a maintainer. A maintainer can comment `/shiplean` to continue.";

    public const string AgentName = "ShipLean";

    public const string RateLimit = "rateLimit";

    public const string RoundCap = "roundCap";

    private static readonly IssuePhase[] Holdable = [IssuePhase.Proposing, IssuePhase.AwaitingReply, IssuePhase.AlreadyShipped];

    // One outbox key per hold transition: the compare-and-set is what dedupes, so the key only has to be new.
    public static string DedupeKey(Guid issueId, string reason, Guid hold) => $"hold:{issueId}:{reason}:{hold}";

    // True when this hold wrote the notices: the caller publishes CommentsChanged for the item after it commits.
    public async Task<bool> HoldAsync(
        GitHubIssueRecord issue, string reason, string explanation, Guid runAsUserId, DbTransaction transaction, CancellationToken cancellationToken)
    {
        if (!await issues.SetPhaseAsync(issue.Id, Holdable, IssuePhase.WaitingForMaintainer, transaction, cancellationToken))
        {
            return false;
        }

        if (issue.OpenOnGitHub)
        {
            await outbox.EnqueueAsync(OutboxEntry.Comment(issue.Id, DedupeKey(issue.Id, reason, Guid.CreateVersion7()), Comment), transaction, cancellationToken);
        }

        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICommentStore>()
            .AddAsAgentAsync(issue.WorkItemId, new CommentCreate(explanation), AgentName, runAsUserId, transaction, cancellationToken);
        return true;
    }
}
