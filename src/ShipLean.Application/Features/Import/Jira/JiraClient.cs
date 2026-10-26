using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;

namespace ShipLean.Application.Features.Import.Jira;

// Jira over HTTP, read-only and stateless: Cloud (REST v3, email + API token as Basic, nextPageToken paging) and
// Server/Data Center (REST v2, personal access token as Bearer, startAt paging). The errors are those of IJiraClient.
public sealed partial class JiraClient(JiraTransport transport, ILogger<JiraClient> logger) : IJiraClient
{
    private const int MaxPageSize = 100;
    private const int ProjectPageSize = 50;
    private const int CommentPageSize = 100;
    private const int CountParallelism = 6;
    public const int MaxProjectPages = 200;
    public const int MaxProjects = 10_000;
    public const int MaxCommentPages = 100;
    public const int MaxComments = 5_000;
    private const string UnknownProjectText = "does not exist for the field 'project'";

    public Task<JiraUser> GetMyselfAsync(JiraCredentials credentials, CancellationToken cancellationToken = default)
        => Reading(async () =>
        {
            var reply = Succeeded(await transport.GetAsync(credentials, Api(credentials, "myself"), cancellationToken));
            return JiraReader.User(reply.Json) ?? throw ApiException.Validation("Jira did not say who the signed-in user is. Check the address.", "baseUrl");
        });

    public Task<IReadOnlyList<JiraProject>> ListProjectsAsync(JiraCredentials credentials, CancellationToken cancellationToken = default)
        => Reading<IReadOnlyList<JiraProject>>(async () =>
        {
            var projects = new List<JsonObject>();
            if (credentials.Kind == JiraKind.Server)
            {
                projects.AddRange(Items(Succeeded(await transport.GetAsync(credentials, Api(credentials, "project"), cancellationToken)).Json));
            }
            else
            {
                var start = 0;
                var pages = 0;
                while (true)
                {
                    var page = Succeeded(await transport.GetAsync(credentials, Api(credentials, $"project/search?startAt={start}&maxResults={ProjectPageSize}"), cancellationToken)).Json;
                    var values = Items(page?["values"]).ToList();
                    projects.AddRange(values);
                    if (values.Count == 0 || (bool?)page?["isLast"] != false)
                    {
                        break;
                    }
                    if (++pages >= MaxProjectPages || projects.Count > MaxProjects)
                    {
                        throw ApiException.NotConfigured($"Jira sent more projects than ShipLean reads (over {MaxProjects} in {MaxProjectPages} pages). Check the address.");
                    }
                    start += ProjectPageSize;
                }
            }
            using var gate = new SemaphoreSlim(CountParallelism);
            return await Task.WhenAll(projects.Select(async project =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    var key = JiraReader.Required(project["key"]);
                    return new JiraProject(key, (string?)project["name"] ?? key, (string?)project["projectTypeKey"] ?? string.Empty, await CountOrZeroAsync(credentials, key, cancellationToken));
                }
                finally
                {
                    gate.Release();
                }
            }));
        });

    public Task<IReadOnlyList<JiraIssueType>> GetIssueTypesAsync(JiraCredentials credentials, string projectKey, CancellationToken cancellationToken = default)
        => Reading<IReadOnlyList<JiraIssueType>>(async () =>
        {
            var reply = await transport.GetAsync(credentials, Api(credentials, $"project/{Key(projectKey)}"), cancellationToken);
            return [.. Items(Succeeded(reply, UnknownProject(projectKey)).Json?["issueTypes"]).Select(t => new JiraIssueType(JiraReader.Required(t["id"]), JiraReader.Required(t["name"]), (bool?)t["subtask"] ?? false))];
        });

    public Task<IReadOnlyList<JiraStatus>> GetStatusesAsync(JiraCredentials credentials, string projectKey, CancellationToken cancellationToken = default)
        => Reading<IReadOnlyList<JiraStatus>>(async () =>
        {
            var reply = await transport.GetAsync(credentials, Api(credentials, $"project/{Key(projectKey)}/statuses"), cancellationToken);
            var byId = new Dictionary<string, (string Name, JiraStatusCategory Category, List<string> Types)>();
            foreach (var type in Items(Succeeded(reply, UnknownProject(projectKey)).Json))
            {
                foreach (var status in Items(type["statuses"]))
                {
                    var id = JiraReader.Required(status["id"]);
                    if (!byId.TryGetValue(id, out var entry))
                    {
                        entry = (JiraReader.Required(status["name"]), Category((string?)status["statusCategory"]?["key"]), []);
                        byId[id] = entry;
                    }
                    entry.Types.Add(JiraReader.Required(type["name"]));
                }
            }
            return [.. byId.Select(s => new JiraStatus(s.Key, s.Value.Name, s.Value.Category, [.. s.Value.Types.Distinct()]))];
        });

    public Task<IReadOnlyList<JiraField>> GetFieldsAsync(JiraCredentials credentials, CancellationToken cancellationToken = default)
        => Reading<IReadOnlyList<JiraField>>(async () => [.. Items(Succeeded(await transport.GetAsync(credentials, Api(credentials, "field"), cancellationToken)).Json)
            .Select(f => new JiraField(JiraReader.Required(f["id"]), (string?)f["name"] ?? JiraReader.Required(f["id"]), (bool?)f["custom"] ?? false, (string?)f["schema"]?["type"]))]);

    public Task<JiraIssuePage> SearchIssuesAsync(JiraCredentials credentials, string projectKey, string? continuation, int pageSize, IReadOnlyList<string> numberFieldIds, CancellationToken cancellationToken = default)
        => Reading(async () =>
        {
            var size = Math.Clamp(pageSize, 1, MaxPageSize);
            var jql = Uri.EscapeDataString($"project = \"{Key(projectKey)}\" ORDER BY key ASC");
            var cloud = credentials.Kind == JiraKind.Cloud;
            var paging = cloud ? CloudPaging(continuation) : "&startAt=" + StartAt(continuation).ToString(CultureInfo.InvariantCulture);
            var path = Api(credentials, (cloud ? "search/jql" : "search") + $"?jql={jql}&fields=*all&expand=changelog&maxResults={size}{paging}");
            var reply = await transport.GetAsync(credentials, path, cancellationToken);
            if (reply.Status == HttpStatusCode.BadRequest && reply.Message?.Contains(UnknownProjectText, StringComparison.Ordinal) == true)
            {
                throw UnknownProject(projectKey);
            }
            var page = Succeeded(reply).Json;
            var issues = Items(page?["issues"]).Select(i => JiraReader.Issue(i, numberFieldIds)).ToList();
            return new JiraIssuePage(issues, cloud ? CloudContinuation(page) : ServerContinuation(page, issues.Count));
        });

    public Task<IReadOnlyList<JiraComment>> GetCommentsAsync(JiraCredentials credentials, string issueKey, CancellationToken cancellationToken = default)
        => Reading<IReadOnlyList<JiraComment>>(async () =>
        {
            var comments = new List<JiraComment>();
            var start = 0;
            var pages = 0;
            while (true)
            {
                var reply = await transport.GetAsync(credentials, Api(credentials, $"issue/{IssueKey(issueKey)}/comment?orderBy=created&startAt={start}&maxResults={CommentPageSize}"), cancellationToken);
                var page = Succeeded(reply, ApiException.NotFound($"Jira has no issue {issueKey}.")).Json;
                var values = Items(page?["comments"]).Select(JiraReader.Comment).ToList();
                comments.AddRange(values);
                if (values.Count == 0 || start + values.Count >= ((int?)page?["total"] ?? 0))
                {
                    return [.. comments.OrderBy(c => c.Created)];
                }
                if (++pages >= MaxCommentPages || comments.Count > MaxComments)
                {
                    throw ApiException.NotConfigured($"Jira sent more comments on {issueKey} than ShipLean reads (over {MaxComments} in {MaxCommentPages} pages).");
                }
                start += CommentPageSize;
            }
        });

    public async Task<Stream> OpenAttachmentAsync(JiraCredentials credentials, JiraAttachment attachment, CancellationToken cancellationToken = default)
    {
        var site = JiraEndpointPolicy.Parse(credentials.BaseUrl, transport.AllowLocal);
        if (!Uri.TryCreate(attachment.ContentUrl, UriKind.Absolute, out var content) || !JiraTransport.SameOrigin(site, content))
        {
            throw ApiException.Validation($"The attachment {attachment.Filename} is not on {site}, so ShipLean will not send the Jira token there.");
        }
        var response = await transport.OpenAsync(credentials, content, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            throw ApiException.NotFound($"Jira no longer has the attachment {attachment.Filename}.");
        }
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            throw ApiException.Validation($"Jira would not give the attachment {attachment.Filename} (HTTP {status}).");
        }
        // Disposing the stream releases the response. Each read has the client's deadline; the bytes are the caller's to cap.
        return transport.Bounded(response, await response.Content.ReadAsStreamAsync(cancellationToken));
    }

    // A project whose count fails (a project the token may list but not search, a slow site) is still listed, with no issues counted.
    private async Task<int> CountOrZeroAsync(JiraCredentials credentials, string projectKey, CancellationToken cancellationToken)
    {
        try
        {
            return await CountAsync(credentials, projectKey, cancellationToken);
        }
        catch (ApiException error)
        {
            logger.LogWarning(error, "Counting the issues of Jira project {ProjectKey} failed ({Code}); listing it without a count", projectKey, error.Code);
            return 0;
        }
    }

    private async Task<int> CountAsync(JiraCredentials credentials, string projectKey, CancellationToken cancellationToken)
    {
        var jql = $"project = \"{Key(projectKey)}\"";
        if (credentials.Kind == JiraKind.Cloud)
        {
            var reply = await transport.PostAsync(credentials, Api(credentials, "search/approximate-count"), new JsonObject { ["jql"] = jql }, cancellationToken);
            return (int?)Succeeded(reply).Json?["count"] ?? 0;
        }
        var path = Api(credentials, $"search?jql={Uri.EscapeDataString(jql)}&maxResults=0&fields=key");
        return (int?)Succeeded(await transport.GetAsync(credentials, path, cancellationToken)).Json?["total"] ?? 0;
    }

    // Jira's answer is read into records here; a field of the wrong kind or a missing one is a site that does not answer like Jira, not a server error.
    private static async Task<T> Reading<T>(Func<Task<T>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception error) when (error is FormatException or InvalidOperationException or InvalidCastException or ArgumentException)
        {
            throw ApiException.NotConfigured("The Jira site answered in a form ShipLean does not understand. Check that the address and the Jira kind are right.");
        }
    }

    private static string? CloudContinuation(JsonNode? page)
        => (bool?)page?["isLast"] == true ? null : (string?)page?["nextPageToken"];

    private static string? ServerContinuation(JsonNode? page, int count)
    {
        var next = ((int?)page?["startAt"] ?? 0) + count;
        return count > 0 && next < ((int?)page?["total"] ?? 0) ? next.ToString(CultureInfo.InvariantCulture) : null;
    }

    private static string CloudPaging(string? continuation) => continuation is null ? string.Empty : "&nextPageToken=" + Uri.EscapeDataString(continuation);

    private static int StartAt(string? continuation)
    {
        if (continuation is null)
        {
            return 0;
        }
        return int.TryParse(continuation, NumberStyles.None, CultureInfo.InvariantCulture, out var start) ? start : throw ApiException.Validation("That page of Jira issues is not valid.");
    }

    private static string Api(JiraCredentials credentials, string route) => $"/rest/api/{(credentials.Kind == JiraKind.Cloud ? 3 : 2)}/{route}";

    private static IEnumerable<JsonObject> Items(JsonNode? node) => (node as JsonArray ?? []).OfType<JsonObject>();

    private static JiraStatusCategory Category(string? key) => key switch
    {
        "indeterminate" => JiraStatusCategory.Indeterminate,
        "done" => JiraStatusCategory.Done,
        _ => JiraStatusCategory.New,
    };

    // A 404 is the caller's "not found"; any other failure that is not a success is Jira refusing the request.
    private static JiraReply Succeeded(JiraReply reply, ApiException? onNotFound = null)
    {
        if (reply.IsSuccess)
        {
            return reply;
        }
        if (reply.Status == HttpStatusCode.NotFound && onNotFound is not null)
        {
            throw onNotFound;
        }
        throw ApiException.Validation(reply.Message ?? $"Jira answered HTTP {(int)reply.Status}. Check the address.");
    }

    private static ApiException UnknownProject(string projectKey) => ApiException.NotFound($"Jira has no project {projectKey}, or these credentials cannot see it.");

    // Keys are interpolated into paths and JQL, so only what Jira allows in one gets through; anything else cannot exist.
    private static string Key(string projectKey)
        => ProjectKeyPattern().IsMatch(projectKey) ? projectKey : throw UnknownProject(projectKey);

    private static string IssueKey(string issueKey)
        => IssueKeyPattern().IsMatch(issueKey) ? issueKey : throw ApiException.NotFound($"Jira has no issue {issueKey}.");

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*\z")]
    private static partial Regex ProjectKeyPattern();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*-\d+\z")]
    private static partial Regex IssueKeyPattern();
}
