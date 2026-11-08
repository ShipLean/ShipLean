namespace ShipLean.Contracts.Webhooks;

// The answer to a webhook delivery, shown in the provider's delivery log: what happened, the keys of the items it linked
// the pull or merge request to, the keys of the items it closed, and for a GitHub issue or issue comment the key of the
// work item the issue is on (null otherwise).
public sealed record WebhookResultDto(WebhookOutcome Outcome, IReadOnlyList<string> Linked, IReadOnlyList<string> Closed, string? Item = null)
{
    public static WebhookResultDto Of(WebhookOutcome outcome) => new(outcome, [], []);
}
