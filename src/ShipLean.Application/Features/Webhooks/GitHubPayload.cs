using System.Text.Json;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// The GitHub parser: X-GitHub-Event "pull_request" with action opened, edited, reopened or closed becomes a
// PullRequestEvent; "ping" (sent when the webhook is added) is answered; anything else is ignored.
internal static class GitHubPayload
{
    private static readonly string[] Actions = ["opened", "edited", "reopened", "closed"];

    public static ParsedDelivery Parse(string? eventName, JsonElement root)
    {
        var repository = root.TryGetProperty("repository", out var repo) ? PayloadJson.OptionalText(repo, "full_name") : null;
        if (eventName == "ping")
        {
            return ParsedDelivery.Answer(WebhookOutcome.Pong, repository);
        }
        if (eventName != "pull_request" || !Actions.Contains(PayloadJson.OptionalText(root, "action")))
        {
            return ParsedDelivery.Answer(WebhookOutcome.Ignored, repository);
        }

        var pull = PayloadJson.Object(root, "pull_request");
        var number = pull.TryGetProperty("number", out var n) && n.TryGetInt32(out var value) ? value : 0;
        var name = repository ?? PayloadJson.Text(PayloadJson.Object(root, "repository"), "full_name");
        return ParsedDelivery.Apply(new PullRequestEvent(
            WebhookProvider.Github,
            PayloadJson.Text(pull, "html_url"),
            StateOf(pull),
            PayloadJson.Text(pull, "title"),
            PayloadJson.OptionalText(PayloadJson.Object(pull, "head"), "ref") ?? string.Empty,
            PayloadJson.Timestamp(pull, "updated_at"),
            name,
            $"GitHub {name}#{number}"));
    }

    private static LinkCardState StateOf(JsonElement pull)
    {
        if (PayloadJson.Flag(pull, "merged"))
        {
            return LinkCardState.Merged;
        }
        return PayloadJson.OptionalText(pull, "state") == "closed" ? LinkCardState.Closed : LinkCardState.Open;
    }
}
