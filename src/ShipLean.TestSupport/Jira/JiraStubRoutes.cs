using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ShipLean.Contracts.Import;

namespace ShipLean.TestSupport.Jira;

// Routes one request of the stub to the raw files and shapes the answer for Cloud or Server.
public sealed partial class JiraStubRoutes
{
    private const int MaxPageSize = 100;
    private const string EpicLinkField = "customfield_10014";

    private readonly JiraKind _kind;
    private readonly string _baseUrl;
    private readonly string? _body;
    private readonly Uri _uri;
    private readonly string _api;
    private readonly bool _serverRedirectsOffSite;
    private readonly Lazy<Dictionary<string, string>> _parameters;

    public JiraStubRoutes(JiraKind kind, string baseUrl, string? body, Uri uri, bool serverRedirectsOffSite)
    {
        (_kind, _baseUrl, _body, _uri, _serverRedirectsOffSite) = (kind, baseUrl, body, uri, serverRedirectsOffSite);
        _api = kind == JiraKind.Cloud ? "3" : "2";
        _parameters = new Lazy<Dictionary<string, string>>(ReadParameters);
    }

    public HttpResponseMessage Answer()
    {
        var path = _uri.AbsolutePath;
        var prefix = $"/rest/api/{_api}/";
        if (path.StartsWith("/secure/attachment/", StringComparison.Ordinal) && _kind == JiraKind.Server)
        {
            var rest = path["/secure/attachment/".Length..];
            return _serverRedirectsOffSite ? RedirectToMedia(rest.Split('/')[0]) : Attachment(rest);
        }
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return NotFound($"No REST resource at {path}.");
        }
        var route = path[prefix.Length..];
        return route switch
        {
            "myself" => Raw($"myself.{(_kind == JiraKind.Cloud ? "cloud" : "server")}.json"),
            "project/search" when _kind == JiraKind.Cloud => Raw("projects.cloud.json"),
            "project" when _kind == JiraKind.Server => Raw("projects.server.json"),
            "field" => Fields(),
            "search/jql" when _kind == JiraKind.Cloud => Search(),
            "search" when _kind == JiraKind.Server => Search(),
            "search/approximate-count" when _kind == JiraKind.Cloud => Count(),
            _ => Keyed(route),
        };
    }

    private HttpResponseMessage Keyed(string route)
    {
        if (Match(ProjectRoute(), route) is { } project)
        {
            var key = project.Groups[1].Value;
            return project.Groups[2].Value switch
            {
                "" => key == JiraStubFiles.ProjectKey ? Raw("project.PLATFORM.json") : NotFound($"No project could be found with key '{key}'."),
                _ => key == JiraStubFiles.ProjectKey ? Raw("statuses.PLATFORM.json") : NotFound($"No project could be found with key '{key}'."),
            };
        }
        if (Match(AttachmentContentRoute(), route) is { } content && _kind == JiraKind.Cloud)
        {
            return AttachmentContent(content.Groups[1].Value);
        }
        if (Match(IssueRoute(), route) is { } issue)
        {
            var key = issue.Groups[1].Value;
            if (!JiraStubFiles.IssueKeys.Contains(key))
            {
                return NotFound($"Issue does not exist or you do not have permission to see it: {key}");
            }
            return issue.Groups[2].Success ? Comments(key) : IssueResponse(key);
        }
        return NotFound($"No REST resource at /rest/api/{_api}/{route}.");
    }

    private HttpResponseMessage IssueResponse(string key)
        => Respond(Shape(JiraStubFiles.ReadJson(_baseUrl, _api, "issues", key + ".json").AsObject(), ExpandsChangelog(), requestedFields: null));

    private HttpResponseMessage Comments(string key)
        => JiraStubFiles.Exists("comments", key + ".json")
            ? Respond(JiraStubFiles.ReadJson(_baseUrl, _api, "comments", key + ".json"))
            : Respond(new JsonObject { ["startAt"] = 0, ["maxResults"] = 50, ["total"] = 0, ["comments"] = new JsonArray() });

    // The media service's answer (the stub handler has already checked the token); the path is /media/{id}/{filename}.
    public static HttpResponseMessage MediaFile(Uri uri) => Attachment(uri.AbsolutePath["/media/".Length..]);

    private static HttpResponseMessage Attachment(string rest)
    {
        var parts = rest.Split('/');
        return parts.Length == 2 && JiraStubFiles.Exists("attachments", parts[0] + ".png")
            ? FileResponse(parts[0])
            : NotFound("The attachment was not found.");
    }

    private static HttpResponseMessage FileResponse(string id)
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(JiraStubFiles.ReadBytes("attachments", id + ".png")) { Headers = { ContentType = new("image/png") } } };

    // Jira Cloud answers the attachment's content URL with a 303 to a media URL on another host (Atlassian's media service); that URL
    // carries its own token and rejects the Authorization header.
    private static HttpResponseMessage AttachmentContent(string id)
        => JiraStubFiles.Exists("attachments", id + ".png") ? RedirectToMedia(id, HttpStatusCode.SeeOther) : NotFound("The attachment was not found.");

    private static HttpResponseMessage RedirectToMedia(string id, HttpStatusCode status = HttpStatusCode.Found)
    {
        if (!JiraStubFiles.Exists("attachments", id + ".png"))
        {
            return NotFound("The attachment was not found.");
        }
        var name = id == JiraStubFiles.AttachmentId ? JiraStubFiles.AttachmentFilename : id + ".png";
        return new HttpResponseMessage(status) { Headers = { Location = new Uri($"https://{JiraStubHandler.MediaHost}/media/{id}/{name}?token={JiraStubHandler.MediaToken}") } };
    }

    private HttpResponseMessage Fields()
    {
        var fields = JiraStubFiles.ReadJson(_baseUrl, _api, "fields.json").AsArray();
        if (_kind == JiraKind.Cloud)
        {
            // Epic Link is a Server/Data Center field; Cloud nests stories under their epic through parent.
            fields.Remove(fields.First(f => (string?)f!["id"] == EpicLinkField));
        }
        return Respond(fields);
    }

    private HttpResponseMessage Count()
        => Respond(new JsonObject { ["count"] = ProjectIssues().Count });

    private HttpResponseMessage Search()
    {
        var jql = Parameter("jql") ?? string.Empty;
        var project = JqlProject().Match(jql);
        if (!project.Success || project.Groups[1].Value is not (JiraStubFiles.ProjectKey or "INFRA"))
        {
            // Jira answers an unknown project with a 400 and this text (an unrelated 400 must not be read as "not found").
            var unknown = project.Success ? project.Groups[1].Value : string.Empty;
            return Json(HttpStatusCode.BadRequest, new JsonObject { ["errorMessages"] = new JsonArray($"The value '{unknown}' does not exist for the field 'project'."), ["errors"] = new JsonObject() });
        }
        var keys = project.Groups[1].Value == JiraStubFiles.ProjectKey ? JiraStubFiles.IssueKeys : [];
        var size = Math.Clamp(int.TryParse(Parameter("maxResults"), CultureInfo.InvariantCulture, out var asked) ? asked : 50, 0, MaxPageSize);
        var start = TokenStart(Parameter("nextPageToken"));
        if (_kind == JiraKind.Server)
        {
            start = int.TryParse(Parameter("startAt"), CultureInfo.InvariantCulture, out var at) ? at : 0;
        }
        if (start < 0)
        {
            return Json(HttpStatusCode.BadRequest, new JsonObject { ["errorMessages"] = new JsonArray("The next page token is not valid."), ["errors"] = new JsonObject() });
        }
        var requested = Parameter("fields")?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var issues = new JsonArray();
        foreach (var key in keys.Skip(start).Take(size))
        {
            var issue = JiraStubFiles.ReadJson(_baseUrl, _api, "issues", key + ".json").AsObject();
            issues.Add(_kind == JiraKind.Cloud && requested is null
                ? new JsonObject { ["id"] = (string?)issue["id"] }
                : Shape(issue, ExpandsChangelog(), requested));
        }
        var next = start + issues.Count;
        var page = new JsonObject();
        if (_kind == JiraKind.Server)
        {
            page["expand"] = "schema,names";
            page["startAt"] = start;
            page["maxResults"] = size;
            page["total"] = keys.Count;
            page["issues"] = issues;
            return Respond(page);
        }
        page["issues"] = issues;
        page["isLast"] = next >= keys.Count;
        if (next < keys.Count)
        {
            page["nextPageToken"] = Convert.ToBase64String(Encoding.UTF8.GetBytes($"idx:{next}"));
        }
        return Respond(page);
    }

    private IReadOnlyList<string> ProjectIssues()
        => JqlProject().Match(Parameter("jql") ?? string.Empty) is { Success: true } project && project.Groups[1].Value == JiraStubFiles.ProjectKey ? JiraStubFiles.IssueKeys : [];

    // Cloud with no fields parameter returns only ids; the changelog is only there when expand names it.
    private JsonObject Shape(JsonObject issue, bool changelog, string[]? requestedFields)
    {
        if (!changelog)
        {
            issue.Remove("changelog");
        }
        if (_kind == JiraKind.Server)
        {
            MoveEpicParentToEpicLink(issue["fields"]!.AsObject());
        }
        if (requestedFields is { Length: > 0 } && !requestedFields.Any(f => f is "*all" or "*navigable"))
        {
            var fields = issue["fields"]!.AsObject();
            foreach (var name in fields.Select(f => f.Key).Where(k => !requestedFields.Contains(k)).ToList())
            {
                fields.Remove(name);
            }
        }
        return _kind == JiraKind.Server ? (JsonObject)ForServer(issue)! : issue;
    }

    // Server/Data Center nests only sub-tasks under a parent; a story under an epic names it in the Epic Link custom field.
    private static void MoveEpicParentToEpicLink(JsonObject fields)
    {
        if (fields["parent"] is JsonObject parent && (string?)parent["fields"]?["issuetype"]?["name"] == "Epic")
        {
            fields[EpicLinkField] = (string?)parent["key"];
            fields["parent"] = null;
        }
    }

    private HttpResponseMessage Raw(string file) => Respond(JiraStubFiles.ReadJson(_baseUrl, _api, file));

    private HttpResponseMessage Respond(JsonNode body) => JiraStubHandler.Json(HttpStatusCode.OK, _kind == JiraKind.Server ? ForServer(body)! : body);

    private static HttpResponseMessage Json(HttpStatusCode status, JsonNode body) => JiraStubHandler.Json(status, body);

    private static HttpResponseMessage NotFound(string message)
        => Json(HttpStatusCode.NotFound, new JsonObject { ["errorMessages"] = new JsonArray(message), ["errors"] = new JsonObject() });

    private bool ExpandsChangelog() => (Parameter("expand") ?? string.Empty).Split(',', StringSplitOptions.TrimEntries).Contains("changelog");

    private string? Parameter(string name) => _parameters.Value.TryGetValue(name, out var value) ? value : null;

    private static int TokenStart(string? token)
    {
        if (token is null)
        {
            return 0;
        }
        try
        {
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            return text.StartsWith("idx:", StringComparison.Ordinal) && int.TryParse(text[4..], CultureInfo.InvariantCulture, out var start) ? start : -1;
        }
        catch (FormatException)
        {
            return -1;
        }
    }

    // Query-string values, then the JSON body's (a POST search); arrays are comma-joined.
    private Dictionary<string, string> ReadParameters()
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in _uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            parameters[Uri.UnescapeDataString(parts[0])] = parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : string.Empty;
        }
        if (!string.IsNullOrWhiteSpace(_body) && JsonNode.Parse(_body) is JsonObject body)
        {
            foreach (var (name, value) in body)
            {
                parameters[name] = value is JsonArray array ? string.Join(',', array.Select(v => (string?)v)) : value?.ToString() ?? string.Empty;
            }
        }
        return parameters;
    }

    // Jira Server sends plain-text descriptions and bodies, and users by name and key instead of accountId.
    private static JsonNode? ForServer(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject element:
                foreach (var name in element.Select(p => p.Key).ToList())
                {
                    if (name is "description" or "body" && element[name] is JsonObject document)
                    {
                        element[name] = JiraAdf.ToText(document);
                    }
                    else if (name == "customfield_10020" && element[name] is JsonArray sprints && sprints.All(s => s is JsonObject))
                    {
                        element[name] = new JsonArray([.. sprints.Select(s => (JsonNode)GreenhopperSprint(s!.AsObject()))]);
                    }
                    else if (name == "content" && element[name] is JsonValue content && element["filename"] is not null)
                    {
                        element[name] = ContentUrl().Replace(content.GetValue<string>(), m => $"{m.Groups[1].Value}/secure/attachment/{m.Groups[2].Value}/{element["filename"]}");
                    }
                    else
                    {
                        ForServer(element[name]);
                    }
                }
                if (element["accountId"] is not null)
                {
                    var login = ((string)element["displayName"]!).ToLowerInvariant().Replace(' ', '.');
                    element.Remove("accountId");
                    element.Remove("accountType");
                    if (element["self"] is JsonValue self)
                    {
                        element["self"] = UserSelf().Replace(self.GetValue<string>(), $"user?username={login}");
                    }
                    element["name"] = login;
                    element["key"] = "JIRAUSER-" + login;
                }
                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    ForServer(item);
                }
                break;
        }
        return node;
    }

    // The string form Jira Server/Data Center serves for a sprint in the Sprint field (the greenhopper toString); a future sprint has no dates.
    private static string GreenhopperSprint(JsonObject sprint)
    {
        var id = (int)sprint["id"]!;
        static string Part(JsonNode? value) => value?.GetValue<string>() ?? "<null>";
        return $"com.atlassian.greenhopper.service.sprint.Sprint@{id * 4099:x}[id={id},rapidViewId=34,state={((string)sprint["state"]!).ToUpperInvariant()},"
            + $"name={(string)sprint["name"]!},startDate={Part(sprint["startDate"])},endDate={Part(sprint["endDate"])},completeDate=<null>,sequence={id},goal={Part(sprint["goal"])}]";
    }

    private static Match? Match(Regex pattern, string text) => pattern.Match(text) is { Success: true } match ? match : null;

    [GeneratedRegex(@"^attachment/content/(\d+)$")]
    private static partial Regex AttachmentContentRoute();

    [GeneratedRegex(@"^(https?://[^/]+)/rest/api/3/attachment/content/(\d+)$")]
    private static partial Regex ContentUrl();

    [GeneratedRegex(@"user\?accountId=.*$")]
    private static partial Regex UserSelf();

    [GeneratedRegex(@"^project/([A-Za-z0-9_]+)(?:/(statuses))?$")]
    private static partial Regex ProjectRoute();

    [GeneratedRegex(@"^issue/([A-Za-z0-9_-]+)(/comment)?$")]
    private static partial Regex IssueRoute();

    [GeneratedRegex(@"project\s*=\s*""?([A-Za-z0-9_]+)""?", RegexOptions.IgnoreCase)]
    private static partial Regex JqlProject();
}
