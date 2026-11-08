using ShipLean.Contracts.Webhooks;

namespace ShipLean.Contracts.IssueIntake;

// How the webhook receiver hands a parsed GitHub issue or issue comment to the issue lanes, and gets the answer for
// GitHub's delivery log (WebhookResultDto.Item is the issue's work item key, when a lane names one).
public interface IIssueRouter
{
    Task<WebhookResultDto> RouteAsync(IssueEvent issue, CancellationToken cancellationToken);

    Task<WebhookResultDto> RouteAsync(IssueCommentEvent comment, CancellationToken cancellationToken);
}
