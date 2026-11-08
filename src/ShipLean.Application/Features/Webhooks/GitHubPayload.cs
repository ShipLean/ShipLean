using System.Text.Json;
using System.Text.RegularExpressions;
using ShipLean.Application.Features.IssueIntake;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Webhooks;

// The GitHub parser: X-GitHub-Event "pull_request" with action opened, edited, reopened or closed becomes a
// PullRequestEvent (with the merge commit when it merged); "issues" with action opened, edited, closed or reopened becomes
// an IssueEvent; "issue_comment" with action created becomes an IssueCommentEvent; "ping" (sent when the webhook is added)
// is answered; anything else is ignored. An issue's repository is lower-cased and its host is repository.html_url's.
internal static partial class GitHubPayload
{
    private static readonly string[] Actions = ["opened", "edited", "reopened", "closed"];

    public static ParsedDelivery Parse(string? eventName, JsonElement root)
    {
        var repository = root.TryGetProperty("repository", out var repo) ? PayloadJson.OptionalText(repo, "full_name") : null;
        if (eventName == "ping")
        {
            return ParsedDelivery.Answer(WebhookOutcome.Pong, repository);
        }
        var action = PayloadJson.OptionalText(root, "action");
        return eventName switch
        {
            "pull_request" when Actions.Contains(action) => ParsedDelivery.Apply(PullRequest(root, repository)),
            "issues" when IssueActionOf(action) is { } issueAction => ParsedDelivery.Route(Issue(root, issueAction)),
            "issue_comment" when action == "created" => ParsedDelivery.Route(Comment(root)),
            _ => ParsedDelivery.Answer(WebhookOutcome.Ignored, repository),
        };
    }

    private static PullRequestEvent PullRequest(JsonElement root, string? repository)
    {
        var pull = PayloadJson.Object(root, "pull_request");
        var number = pull.TryGetProperty("number", out var n) && n.TryGetInt32(out var value) ? value : 0;
        var name = repository ?? PayloadJson.Text(PayloadJson.Object(root, "repository"), "full_name");
        var state = StateOf(pull);
        var merged = state == LinkCardState.Merged;
        return new PullRequestEvent(
            WebhookProvider.Github,
            PayloadJson.Text(pull, "html_url"),
            state,
            PayloadJson.Text(pull, "title"),
            PayloadJson.OptionalText(PayloadJson.Object(pull, "head"), "ref") ?? string.Empty,
            PayloadJson.OptionalText(pull, "body") ?? string.Empty,
            PayloadJson.Timestamp(pull, "updated_at"),
            name,
            $"GitHub {name}#{number}",
            merged ? MergeCommitOf(pull) : null,
            merged && PayloadJson.OptionalText(pull, "merged_at") is not null ? PayloadJson.Timestamp(pull, "merged_at") : null);
    }

    // GitHub sends merge_commit_sha for open pull requests too (its test merge), so it is read only once merged.
    private static string? MergeCommitOf(JsonElement pull)
    {
        var sha = PayloadJson.OptionalText(pull, "merge_commit_sha")?.ToLowerInvariant();
        return sha is null || Sha().IsMatch(sha) ? sha : throw ApiException.Validation("The payload's merge_commit_sha is not a commit.");
    }

    private static IssueAction? IssueActionOf(string? action) => action switch
    {
        "opened" => IssueAction.Opened,
        "edited" => IssueAction.Edited,
        "closed" => IssueAction.Closed,
        "reopened" => IssueAction.Reopened,
        _ => null,
    };

    private static IssueEvent Issue(JsonElement root, IssueAction action)
    {
        var issue = PayloadJson.Object(root, "issue");
        var sender = PayloadJson.Object(root, "sender");
        return new IssueEvent(
            action,
            RefOf(root, issue),
            PayloadJson.Text(issue, "html_url"),
            PayloadJson.Text(issue, "title"),
            IssueCommentClassifier.Cap(PayloadJson.OptionalText(issue, "body") ?? string.Empty),
            PayloadJson.Text(PayloadJson.Object(issue, "user"), "login"),
            PayloadJson.Text(issue, "author_association"),
            LabelsOf(issue),
            PayloadJson.Text(issue, "state") == "open",
            PayloadJson.OptionalText(issue, "state_reason"),
            PayloadJson.Text(sender, "login"),
            IsBot(sender),
            PayloadJson.Timestamp(issue, "updated_at"));
    }

    private static IssueCommentEvent Comment(JsonElement root)
    {
        var issue = PayloadJson.Object(root, "issue");
        var comment = PayloadJson.Object(root, "comment");
        var author = PayloadJson.Object(comment, "user");
        return new IssueCommentEvent(
            RefOf(root, issue),
            PayloadJson.Text(issue, "html_url"),
            PayloadJson.Text(issue, "title"),
            PayloadJson.Text(PayloadJson.Object(issue, "user"), "login"),
            PayloadJson.Text(issue, "state") == "open",
            issue.TryGetProperty("pull_request", out var pull) && pull.ValueKind == JsonValueKind.Object,
            comment.TryGetProperty("id", out var id) && id.TryGetInt64(out var commentId) ? commentId : throw ApiException.Validation("The payload has no comment id."),
            PayloadJson.Text(comment, "html_url"),
            IssueCommentClassifier.Cap(PayloadJson.OptionalText(comment, "body") ?? string.Empty),
            PayloadJson.Text(author, "login"),
            PayloadJson.Text(comment, "author_association"),
            IsBot(PayloadJson.Object(root, "sender")) || IsBot(author),
            PayloadJson.Timestamp(comment, "created_at"));
    }

    private static IssueRef RefOf(JsonElement root, JsonElement issue)
    {
        var repository = PayloadJson.Object(root, "repository");
        var htmlUrl = PayloadJson.Text(repository, "html_url");
        var host = Uri.TryCreate(htmlUrl, UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : throw ApiException.Validation("The payload's repository html_url is not a URL.");
        var number = issue.TryGetProperty("number", out var n) && n.TryGetInt32(out var value) ? value : throw ApiException.Validation("The payload has no issue number.");
        return new IssueRef(host, PayloadJson.Text(repository, "full_name").ToLowerInvariant(), number);
    }

    // GitHub sends labels as objects with a name; the REST schema also allows plain strings.
    private static IReadOnlyList<string> LabelsOf(JsonElement issue)
    {
        if (!issue.TryGetProperty("labels", out var labels) || labels.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return [.. labels.EnumerateArray()
            .Select(label => label.ValueKind == JsonValueKind.String ? label.GetString() : PayloadJson.OptionalText(label, "name"))
            .OfType<string>()];
    }

    private static bool IsBot(JsonElement user) => PayloadJson.OptionalText(user, "type") == "Bot";

    private static LinkCardState StateOf(JsonElement pull)
    {
        if (PayloadJson.Flag(pull, "merged"))
        {
            return LinkCardState.Merged;
        }
        return PayloadJson.OptionalText(pull, "state") == "closed" ? LinkCardState.Closed : LinkCardState.Open;
    }

    [GeneratedRegex("^[0-9a-f]{7,64}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sha();
}
