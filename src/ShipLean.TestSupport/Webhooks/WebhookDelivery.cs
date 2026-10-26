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

    public string DeliveryId => Headers.TryGetValue("X-GitHub-Delivery", out var github) ? github : Headers["X-Gitlab-Event-UUID"];

    public static string GithubUrl(int number) => $"https://github.com/{GithubRepository}/pull/{number}";

    public static string GitlabUrl(int iid) => $"https://gitlab.example.com/{GitlabRepository}/-/merge_requests/{iid}";

    // action: opened, edited, reopened, closed. state "open" or "closed"; merged only with closed.
    public static WebhookDelivery Github(
        string secret, string action, int number, string title, DateTimeOffset updatedAt, string branch = "fix/rate-grid", string state = "open", bool merged = false)
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
        pull["updated_at"] = updatedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        pull["head"]!["ref"] = branch;
        return Signed(secret, payload.ToJsonString(), "pull_request");
    }

    // GitHub's ping when a webhook is added.
    public static WebhookDelivery GithubPing(string secret) => Signed(secret, """{"zen":"Keep it logically awesome.","hook_id":1}""", "ping");

    // action: open, update, reopen, merge, close. state: opened, closed, merged.
    public static WebhookDelivery Gitlab(
        string token, string action, int iid, string title, DateTimeOffset updatedAt, string branch = "fix/rate-grid", string state = "opened")
    {
        var payload = Load("gitlab-merge-request.json");
        var request = payload["object_attributes"]!;
        request["action"] = action;
        request["iid"] = iid;
        request["url"] = GitlabUrl(iid);
        request["title"] = title;
        request["state"] = state;
        request["source_branch"] = branch;
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

    private static JsonNode Load(string name)
        => JsonNode.Parse(File.ReadAllText(System.IO.Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "Webhooks", "Payloads", name)))!;
}
