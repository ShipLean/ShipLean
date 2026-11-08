using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace ShipLean.TestSupport.Webhooks;

// A delivery as GitHub or GitLab sends it: one of the recorded payloads in Payloads/ with the fields a test varies set,
// the event header, a fresh delivery id and the signature (GitHub) or token (GitLab). ToRequest() posts it with
// HttpClient; Path, Body and Headers drive Playwright's request context.
public sealed record WebhookDelivery(string Path, string Body, IReadOnlyDictionary<string, string> Headers)
{
    public const string GithubPath = "api/webhooks/github";
    public const string GitlabPath = "api/webhooks/gitlab";
    public const string GithubRepository = "acme/storefront";
    public const string GitlabRepository = "acme/shop/storefront";

    // The recorded issue payloads name the repository "Acme/Storefront"; ShipLean lower-cases it to GithubRepository.
    public const string IssueAuthor = "rita-reporter";
    public const string Maintainer = "maya-maintainer";

    public string DeliveryId => Headers.TryGetValue("X-GitHub-Delivery", out var github) ? github : Headers["X-Gitlab-Event-UUID"];

    public static string GithubUrl(int number) => $"https://github.com/{GithubRepository}/pull/{number}";

    public static string GitlabUrl(int iid) => $"https://gitlab.example.com/{GitlabRepository}/-/merge_requests/{iid}";

    public static string GithubIssueUrl(int number) => $"https://github.com/Acme/Storefront/issues/{number}";

    // action: opened, edited, reopened, closed. state "open" or "closed"; merged only with closed. body null keeps the recorded one.
    // mergeCommitSha and the merge time (updatedAt) are sent only when merged.
    public static WebhookDelivery Github(
        string secret, string action, int number, string title, DateTimeOffset updatedAt, string branch = "fix/rate-grid", string state = "open", bool merged = false, string? body = null,
        string? mergeCommitSha = null)
    {
        var payload = Load("github-pull-request.json");
        payload["action"] = action;
        payload["number"] = number;
        var pull = payload["pull_request"]!;
        pull["number"] = number;
        pull["html_url"] = GithubUrl(number);
        pull["title"] = title;
        pull["state"] = state;
        pull["merged"] = merged;
        pull["updated_at"] = Timestamp(updatedAt);
        pull["head"]!["ref"] = branch;
        pull["merge_commit_sha"] = mergeCommitSha;
        pull["merged_at"] = merged ? Timestamp(updatedAt) : null;
        if (body is not null)
        {
            pull["body"] = body;
        }
        return Signed(secret, payload.ToJsonString(), "pull_request");
    }

    // An "issues" delivery: action opened, edited, closed, reopened (or another, which ShipLean ignores). state "open" or
    // "closed". body and labels null keep the recorded ones. senderType "Bot" for an app.
    public static WebhookDelivery GithubIssue(
        string secret, string action, int number = 64, string title = "Filter the backlog by Epic", string author = IssueAuthor, string association = "NONE",
        string? body = null, string state = "open", IReadOnlyList<string>? labels = null, string senderType = "User")
    {
        var payload = Load("github-issues.json");
        payload["action"] = action;
        var issue = payload["issue"]!;
        SetIssue(issue, number, title, author, state);
        issue["author_association"] = association;
        if (body is not null)
        {
            issue["body"] = body;
        }
        if (labels is not null)
        {
            issue["labels"] = new JsonArray([.. labels.Select(name => (JsonNode)new JsonObject { ["name"] = name })]);
        }
        payload["sender"] = User(author, senderType);
        return Signed(secret, payload.ToJsonString(), "issues");
    }

    // An "issue_comment" delivery on issue number by author (with GitHub's author_association), on a pull request when
    // onPullRequest. authorType "Bot" for an app's comment. action other than created is ignored by ShipLean.
    public static WebhookDelivery GithubIssueComment(
        string secret, string body, string author = Maintainer, string association = "MEMBER", int number = 64, string issueAuthor = IssueAuthor,
        bool onPullRequest = false, string authorType = "User", long commentId = 2470000001, string action = "created")
    {
        var payload = Load("github-issue-comment.json");
        payload["action"] = action;
        var issue = payload["issue"]!;
        SetIssue(issue, number, "Filter the backlog by Epic", issueAuthor, "open");
        if (onPullRequest)
        {
            issue["pull_request"] = new JsonObject
            {
                ["url"] = $"https://api.github.com/repos/Acme/Storefront/pulls/{number}",
                ["html_url"] = $"https://github.com/Acme/Storefront/pull/{number}",
                ["merged_at"] = null,
            };
        }
        var comment = payload["comment"]!;
        comment["id"] = commentId;
        comment["html_url"] = $"{GithubIssueUrl(number)}#issuecomment-{commentId}";
        comment["body"] = body;
        comment["author_association"] = association;
        comment["user"] = User(author, authorType);
        payload["sender"] = User(author, authorType);
        return Signed(secret, payload.ToJsonString(), "issue_comment");
    }

    // GitHub's ping when a webhook is added.
    public static WebhookDelivery GithubPing(string secret) => Signed(secret, """{"zen":"Keep it logically awesome.","hook_id":1}""", "ping");

    // action: open, update, reopen, merge, close. state: opened, closed, merged. description null keeps the recorded one.
    public static WebhookDelivery Gitlab(
        string token, string action, int iid, string title, DateTimeOffset updatedAt, string branch = "fix/rate-grid", string state = "opened", string? description = null)
    {
        var payload = Load("gitlab-merge-request.json");
        var request = payload["object_attributes"]!;
        request["action"] = action;
        request["iid"] = iid;
        request["url"] = GitlabUrl(iid);
        request["title"] = title;
        request["state"] = state;
        request["source_branch"] = branch;
        if (description is not null)
        {
            request["description"] = description;
        }
        request["updated_at"] = updatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
        return new WebhookDelivery(GitlabPath, payload.ToJsonString(), new Dictionary<string, string>
        {
            ["X-Gitlab-Event"] = "Merge Request Hook",
            ["X-Gitlab-Event-UUID"] = Guid.NewGuid().ToString(),
            ["X-Gitlab-Token"] = token,
        });
    }

    // The same delivery with one header replaced, or removed when value is null (a bad or missing signature).
    public WebhookDelivery WithHeader(string name, string? value)
    {
        var headers = Headers.Where(h => h.Key != name).ToDictionary(h => h.Key, h => h.Value);
        if (value is not null)
        {
            headers[name] = value;
        }
        return this with { Headers = headers };
    }

    public HttpRequestMessage ToRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = new StringContent(Body, Encoding.UTF8, "application/json") };
        foreach (var (name, value) in Headers)
        {
            request.Headers.Add(name, value);
        }
        return request;
    }

    private static WebhookDelivery Signed(string secret, string body, string eventName)
        => new(GithubPath, body, new Dictionary<string, string>
        {
            ["X-GitHub-Event"] = eventName,
            ["X-GitHub-Delivery"] = Guid.NewGuid().ToString(),
            ["X-Hub-Signature-256"] = "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))),
        });

    private static void SetIssue(JsonNode issue, int number, string title, string author, string state)
    {
        issue["number"] = number;
        issue["html_url"] = GithubIssueUrl(number);
        issue["title"] = title;
        issue["state"] = state;
        issue["user"] = User(author, "User");
    }

    private static JsonObject User(string login, string type) => new() { ["login"] = login, ["id"] = 5551299, ["type"] = type, ["site_admin"] = false };

    private static string Timestamp(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static JsonNode Load(string name)
        => JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "Webhooks", "Payloads", name)))!;
}
