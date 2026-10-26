using ShipLean.Contracts.Webhooks;

namespace ShipLean.Application.Features.Webhooks;

// What a parser made of a delivery: a pull or merge request event to apply, or the outcome to answer straight away
// (a ping, an event or action ShipLean ignores). Repository is recorded with the delivery either way.
internal sealed record ParsedDelivery(PullRequestEvent? Event, WebhookOutcome Outcome, string? Repository)
{
    public static ParsedDelivery Apply(PullRequestEvent pullRequest) => new(pullRequest, WebhookOutcome.Processed, pullRequest.Repository);

    public static ParsedDelivery Answer(WebhookOutcome outcome, string? repository) => new(null, outcome, repository);
}
