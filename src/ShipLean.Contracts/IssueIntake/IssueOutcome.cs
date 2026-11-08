using ShipLean.Contracts.Webhooks;

namespace ShipLean.Contracts.IssueIntake;

// What a lane did with an issue event: Processed or Ignored, and the key of the work item the issue is on (its Triage
// item, or the item an acceptance pointed it at), when there is one. It becomes WebhookResultDto.Item.
public sealed record IssueOutcome(WebhookOutcome Outcome, string? Item)
{
    public static readonly IssueOutcome Ignored = new(WebhookOutcome.Ignored, null);

    public static IssueOutcome Processed(string? item) => new(WebhookOutcome.Processed, item);
}
