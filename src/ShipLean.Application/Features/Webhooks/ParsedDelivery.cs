using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Webhooks;

namespace ShipLean.Application.Features.Webhooks;

// What a parser made of a delivery: a pull or merge request event to apply, a GitHub issue or issue comment to route, or
// the outcome to answer straight away (a ping, an event or action ShipLean ignores). Repository and Url are recorded with
// the delivery either way.
internal sealed record ParsedDelivery(PullRequestEvent? Event, WebhookOutcome Outcome, string? Repository)
{
    public IssueEvent? Issue { get; private init; }

    public IssueCommentEvent? Comment { get; private init; }

    public string? Url => Event?.Url ?? Issue?.Url ?? Comment?.CommentUrl;

    public static ParsedDelivery Apply(PullRequestEvent pullRequest) => new(pullRequest, WebhookOutcome.Processed, pullRequest.Repository);

    public static ParsedDelivery Route(IssueEvent issue) => new(null, WebhookOutcome.Processed, issue.Issue.Repository) { Issue = issue };

    public static ParsedDelivery Route(IssueCommentEvent comment) => new(null, WebhookOutcome.Processed, comment.Issue.Repository) { Comment = comment };

    public static ParsedDelivery Answer(WebhookOutcome outcome, string? repository) => new(null, outcome, repository);
}
