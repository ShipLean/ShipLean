using System.Net;
using System.Text;

namespace ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

// A stub for the Tempo and Loki clients whose answers a test scripts per request, and which keeps what was asked.
public sealed class ScriptedStores(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> answer) : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> seen = [];

    public IReadOnlyList<HttpRequestMessage> Requests => seen;

    public int Count(string pathPart) => seen.Count(r => r.RequestUri!.AbsolutePath.Contains(pathPart, StringComparison.Ordinal));

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        seen.Add(request);
        return Task.FromResult(answer(request, cancellationToken));
    }
}

// The JSON bodies Tempo and Loki answer with, in the shapes the sources read.
public static class StoreJson
{
    public const string StartNanos = "1763197200000000000";
    public const string EndNanos = "1763197260000000000";

    public static string Search(IEnumerable<(string Trace, string? Session)> hits)
        => "{\"traces\":[" + string.Join(",", hits.Select(h => $"{{\"traceID\":\"{h.Trace}\"" + (h.Session is null ? "}" : $",\"spanSets\":[{{\"spans\":[{{\"spanID\":\"a\",\"attributes\":[{{\"key\":\"session.id\",\"value\":{{\"stringValue\":\"{h.Session}\"}}}}]}}]}}]}}"))) + "]}";

    public static string Trace(string traceId, string project, string item, string session)
        => "{\"batches\":[{\"resource\":{\"attributes\":[" + Attribute("shiplean.project", project) + "," + Attribute("shiplean.item", item) + "," + Attribute("session.id", session) + "]},"
           + $"\"scopeSpans\":[{{\"spans\":[{{\"traceId\":\"{traceId}\",\"spanId\":\"{traceId}1\",\"name\":\"work\",\"startTimeUnixNano\":\"{StartNanos}\",\"endTimeUnixNano\":\"{EndNanos}\"}}]}}]}}]}}";

    public static string Loki(string project, string session, string? traceId)
        => "{\"data\":{\"result\":[{\"stream\":{\"service_name\":\"claude-code\",\"shiplean_project\":\"" + project + "\",\"session_id\":\"" + session + "\""
           + (traceId is null ? "" : ",\"trace_id\":\"" + traceId + "\"") + "},\"values\":[[\"" + StartNanos + "\",\"a line\"]]}]}}";

    public const string EmptyLoki = "{\"data\":{\"result\":[]}}";

    private static string Attribute(string key, string value) => $"{{\"key\":\"{key}\",\"value\":{{\"stringValue\":\"{value}\"}}}}";
}
