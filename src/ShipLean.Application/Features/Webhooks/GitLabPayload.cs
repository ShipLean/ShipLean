using System.Text.Json;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// The GitLab parser: X-Gitlab-Event "Merge Request Hook" with action open, update, reopen, merge or close becomes a
// PullRequestEvent (with the merge commit and merge time when it merged and the payload has them); anything else (approvals, other hooks) is ignored.
internal static class GitLabPayload
{
    private static readonly string[] Actions = ["open", "update", "reopen", "merge", "close"];

    public static ParsedDelivery Parse(string? eventName, JsonElement root)
    {
        var repository = root.TryGetProperty("project", out var project) ? PayloadJson.OptionalText(project, "path_with_namespace") : null;
        if (eventName != "Merge Request Hook")
        {
            return ParsedDelivery.Answer(WebhookOutcome.Ignored, repository);
        }

        var request = PayloadJson.Object(root, "object_attributes");
        var action = PayloadJson.OptionalText(request, "action");
        if (!Actions.Contains(action))
        {
            return ParsedDelivery.Answer(WebhookOutcome.Ignored, repository);
        }

        var iid = request.TryGetProperty("iid", out var n) && n.TryGetInt32(out var value) ? value : 0;
        var name = repository ?? PayloadJson.Text(PayloadJson.Object(root, "project"), "path_with_namespace");
        var state = StateOf(PayloadJson.OptionalText(request, "state"), action);
        var merged = state == LinkCardState.Merged;
        return ParsedDelivery.Apply(new PullRequestEvent(
            WebhookProvider.Gitlab,
            PayloadJson.Text(request, "url"),
            state,
            PayloadJson.Text(request, "title"),
            PayloadJson.OptionalText(request, "source_branch") ?? string.Empty,
            PayloadJson.OptionalText(request, "description") ?? string.Empty,
            PayloadJson.Timestamp(request, "updated_at"),
            name,
            $"GitLab {name}!{iid}",
            merged ? GitHubPayload.MergeCommitOf(request) : null,
            merged && PayloadJson.OptionalText(request, "merged_at") is not null ? PayloadJson.Timestamp(request, "merged_at") : null));
    }

    // The state field ("opened", "closed", "merged", "locked"), or the action when a payload leaves it out.
    private static LinkCardState StateOf(string? state, string? action) => (state, action) switch
    {
        ("merged", _) or (null, "merge") => LinkCardState.Merged,
        ("closed", _) or (null, "close") => LinkCardState.Closed,
        _ => LinkCardState.Open,
    };
}
