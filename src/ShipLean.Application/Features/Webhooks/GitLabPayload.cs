using System.Text.Json;
using System.Text.RegularExpressions;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// The GitLab parser: X-Gitlab-Event "Merge Request Hook" with action open, update, reopen, merge or close becomes a
// PullRequestEvent (with the merge commit and merge time when it merged and the payload has them); anything else (approvals, other hooks) is ignored.
internal static partial class GitLabPayload
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
            merged ? MergeCommitOf(request) : null,
            merged && PayloadJson.OptionalText(request, "merged_at") is not null ? PayloadJson.Timestamp(request, "merged_at") : null));
    }

    // merge_commit_sha is null until the request merges; a value that is not a commit is refused, as for GitHub.
    private static string? MergeCommitOf(JsonElement request)
    {
        var sha = PayloadJson.OptionalText(request, "merge_commit_sha")?.ToLowerInvariant();
        return sha is null || Sha().IsMatch(sha) ? sha : throw ApiException.Validation("The payload's merge_commit_sha is not a commit.");
    }

    [GeneratedRegex("^[0-9a-f]{7,64}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sha();

    // The state field ("opened", "closed", "merged", "locked"), or the action when a payload leaves it out.
    private static LinkCardState StateOf(string? state, string? action) => (state, action) switch
    {
        ("merged", _) or (null, "merge") => LinkCardState.Merged,
        ("closed", _) or (null, "close") => LinkCardState.Closed,
        _ => LinkCardState.Open,
    };
}
