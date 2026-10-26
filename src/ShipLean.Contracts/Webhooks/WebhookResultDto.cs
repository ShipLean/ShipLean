namespace ShipLean.Contracts.Webhooks;

// The answer to a webhook delivery, shown in the provider's delivery log: what happened, the keys of the items it linked
// the pull or merge request to, and the keys of the items it closed.
public sealed record WebhookResultDto(WebhookOutcome Outcome, IReadOnlyList<string> Linked, IReadOnlyList<string> Closed)
{
    public static WebhookResultDto Of(WebhookOutcome outcome) => new(outcome, [], []);
}
