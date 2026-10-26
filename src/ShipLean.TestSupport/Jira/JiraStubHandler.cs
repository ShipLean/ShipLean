using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ShipLean.Contracts.Import;

namespace ShipLean.TestSupport.Jira;

// An in-process Jira for the sample project PLATFORM: Jira Cloud (v3, Basic email:token, nextPageToken paging) at CloudUrl and
// Jira Server/Data Center (v2, Bearer token, startAt paging) at ServerUrl, both from the same raw files. Read-only, like ShipLean.
// IsDown answers 503 to everything; a wrong or missing token answers 401. A real client is built on HttpClient(new JiraStubHandler()).
public sealed partial class JiraStubHandler : HttpMessageHandler
{
    public const string CloudHost = "platform.atlassian.net";
    public const string ServerHost = "jira.platform.example";
    public const string CloudEmail = "priya.shah@acme.io";
    public const string CloudToken = "cloud-api-token-3fA9";
    public const string ServerToken = "server-pat-k7Qe";

    // Atlassian's media service: the 303 target of a Cloud attachment's content URL, another host. It answers 401 to any request that carries an
    // Authorization header (the Jira token must never get there) and serves the bytes to one that carries MediaToken in its query instead.
    public const string MediaHost = "media.platform-stub.example";
    public const string MediaToken = "media-7c1d";
    public static readonly string CloudUrl = "https://" + CloudHost;
    public static readonly string ServerUrl = "https://" + ServerHost;

    private readonly object _gate = new();
    private readonly List<string> _requests = [];
    private volatile bool _down;
    private volatile bool _serverRedirectsOffSite;

    // Makes Server answer an attachment's content URL with a 302 to the media host too, as a Server behind a CDN or file proxy does.
    public bool ServerRedirectsAttachmentsOffSite
    {
        get => _serverRedirectsOffSite;
        set => _serverRedirectsOffSite = value;
    }

    public bool IsDown
    {
        get => _down;
        set => _down = value;
    }

    // "GET /rest/api/3/search/jql?..." for every request received, oldest first.
    public IReadOnlyList<string> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    public int RequestCount => Requests.Count;

    // Search calls only, to assert paging.
    public int SearchRequestCount => Requests.Count(r => SearchPath().IsMatch(r));

    public static JiraCredentials Credentials(JiraKind kind, string? token = null) => kind == JiraKind.Cloud
        ? new JiraCredentials(CloudUrl, kind, CloudEmail, token ?? CloudToken)
        : new JiraCredentials(ServerUrl, kind, string.Empty, token ?? ServerToken);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri ?? throw new InvalidOperationException("The stub needs an absolute request URI.");
        lock (_gate)
        {
            _requests.Add($"{request.Method} {uri.PathAndQuery}");
        }
        if (uri.Host == MediaHost)
        {
            return Media(request, uri);
        }
        var kind = uri.Host switch
        {
            CloudHost => JiraKind.Cloud,
            ServerHost => JiraKind.Server,
            _ => throw new HttpRequestException($"No such host is known: {uri.Host}"),
        };
        if (_down)
        {
            return Json(HttpStatusCode.ServiceUnavailable, new JsonObject { ["errorMessages"] = new JsonArray("Service Unavailable") });
        }
        if (!IsAuthorised(request.Headers.Authorization, kind))
        {
            return Json(HttpStatusCode.Unauthorized, Unauthorised(kind));
        }
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new JiraStubRoutes(kind, uri.GetLeftPart(UriPartial.Authority), body, uri, _serverRedirectsOffSite).Answer();
    }

    private HttpResponseMessage Media(HttpRequestMessage request, Uri uri)
    {
        if (_down)
        {
            return Json(HttpStatusCode.ServiceUnavailable, new JsonObject { ["message"] = "Service Unavailable" });
        }
        if (request.Headers.Authorization is not null || !uri.Query.Contains("token=" + MediaToken, StringComparison.Ordinal))
        {
            return Json(HttpStatusCode.Unauthorized, new JsonObject { ["message"] = "The media service does not accept the Jira token.", ["status-code"] = 401 });
        }
        return JiraStubRoutes.MediaFile(uri);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, JsonNode body)
        => new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private static bool IsAuthorised(AuthenticationHeaderValue? header, JiraKind kind)
    {
        if (header?.Parameter is null)
        {
            return false;
        }
        return kind == JiraKind.Cloud
            ? header.Scheme == "Basic" && header.Parameter == Convert.ToBase64String(Encoding.UTF8.GetBytes($"{CloudEmail}:{CloudToken}"))
            : header.Scheme == "Bearer" && header.Parameter == ServerToken;
    }

    private static JsonObject Unauthorised(JiraKind kind) => kind == JiraKind.Cloud
        ? new JsonObject { ["message"] = "Client must be authenticated to access this resource.", ["status-code"] = 401 }
        : new JsonObject { ["errorMessages"] = new JsonArray("You are not authenticated. Authentication required to perform this operation."), ["errors"] = new JsonObject() };

    [GeneratedRegex(@"search(/jql)?(\?|$)")]
    private static partial Regex SearchPath();
}
