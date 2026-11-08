using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Webhooks;

namespace ShipLean.Application.Features.IssueIntake;

// Sends a parsed issue or issue comment to its lane. "opened" goes to intake's OpenedAsync, the other issue actions to
// ChangedAsync. A comment is classified against ShipLean's own GitHub login, then a Command goes to intake's TakeAsync, an
// Accept to acceptance, a Reply or a Mirror to replies, and an Ignore goes nowhere. The delivery dedupe runs before this,
// but the lanes don't rely on it: each is idempotent (D18).
public sealed class IssueRouter(IGitHubIdentity identity, IIssueIntake intake, IIssueReplies replies, IIssueAcceptance acceptance) : IIssueRouter
{
    public async Task<WebhookResultDto> RouteAsync(IssueEvent issue, CancellationToken cancellationToken)
    {
        var outcome = issue.Action == IssueAction.Opened
            ? await intake.OpenedAsync(issue, cancellationToken)
            : await intake.ChangedAsync(issue, cancellationToken);
        return Result(outcome);
    }

    public async Task<WebhookResultDto> RouteAsync(IssueCommentEvent comment, CancellationToken cancellationToken)
    {
        if (comment.OnPullRequest)
        {
            return Result(IssueOutcome.Ignored);
        }

        var kind = IssueCommentClassifier.Classify(comment, await identity.SelfLoginAsync(cancellationToken));
        var outcome = kind switch
        {
            IssueCommentKind.Command => await intake.TakeAsync(comment, cancellationToken),
            IssueCommentKind.Accept => await acceptance.AcceptAsync(comment, cancellationToken),
            IssueCommentKind.Reply => await replies.ReplyAsync(comment, cancellationToken),
            IssueCommentKind.Mirror => await replies.MirrorAsync(comment, cancellationToken),
            _ => IssueOutcome.Ignored,
        };
        return Result(outcome);
    }

    private static WebhookResultDto Result(IssueOutcome outcome) => new(outcome.Outcome, [], [], outcome.Item);
}
