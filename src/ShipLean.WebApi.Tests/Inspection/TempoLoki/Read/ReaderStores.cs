using System.Net;
using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.TempoLoki;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

namespace ShipLean.WebApi.Tests.Inspection.TempoLoki.Read;

// Canned Tempo (trace by id, v2) and Loki answers for the run reader, and the reader built over a stub.
public static class ReaderStores
{
    public static readonly DateTimeOffset Now = new(2025, 11, 15, 9, 5, 0, TimeSpan.Zero);

    public const string RootId = "AAAAAAAAEAE=";
    public const string ChildId = "AAAAAAAAEAI=";

    public static readonly InspectionOptions Options = new() { TempoUrl = "http://tempo.test", LokiUrl = "http://loki.test" };

    public static RunRef Ref(string item = "SHIP-1", string run = "s1") => new("SHIP", item, run, Now.AddDays(-1), Now, null, null);

    public static TempoLokiRunReader Reader(HttpMessageHandler stores, InspectionOptions? options = null, TimeProvider? time = null)
    {
        var settings = options ?? Options;
        var clients = StoreClients.Create(settings, stores);
        return new TempoLokiRunReader(new TempoTraceLoader(clients, settings), new LokiLogReader(new LokiRunFinder(clients, settings), settings), settings, time ?? TimeProvider.System);
    }

    // Tempo's v2 answer: ids are base64, the root has no parent.
    public static string Trace(string project = "SHIP", string item = "SHIP-1", string session = "s1", string status = "complete")
        => "{\"status\":\"" + status + "\",\"trace\":{\"resourceSpans\":[{\"resource\":{\"attributes\":[" + Attribute("shiplean.project", project) + "," + Attribute("shiplean.item", item) + "," + Attribute("session.id", session) + "]},"
           + "\"scopeSpans\":[{\"spans\":["
           + $"{{\"spanId\":\"{RootId}\",\"name\":\"root\",\"kind\":\"SPAN_KIND_INTERNAL\",\"startTimeUnixNano\":\"{StoreJson.StartNanos}\",\"endTimeUnixNano\":\"{StoreJson.EndNanos}\",\"status\":{{}}}},"
           + $"{{\"spanId\":\"{ChildId}\",\"parentSpanId\":\"{RootId}\",\"name\":\"child\",\"kind\":\"SPAN_KIND_CLIENT\",\"startTimeUnixNano\":\"{StoreJson.StartNanos}\",\"endTimeUnixNano\":\"{StoreJson.EndNanos}\",\"status\":{{\"code\":\"STATUS_CODE_ERROR\",\"message\":\"boom\"}}}}"
           + "]}]}]}}";

    public static string Attribute(string key, string value) => $"{{\"key\":\"{key}\",\"value\":{{\"stringValue\":\"{value}\"}}}}";

    public static string LokiLines(int count, string project = "SHIP", string session = "s1", string? traceId = null)
        => "{\"data\":{\"result\":[{\"stream\":{\"service_name\":\"claude-code\",\"shiplean_project\":\"" + project + "\",\"shiplean_item\":\"SHIP-1\",\"session_id\":\"" + session + "\""
           + (traceId is null ? "" : ",\"trace_id\":\"" + traceId + "\"") + "},\"values\":["
           + string.Join(",", Enumerable.Range(0, count).Select(i => $"[\"{long.Parse(StoreJson.StartNanos) + i}\",\"line {i}\"]")) + "]}]}}";

    public static ScriptedStores Stores(Func<string, string> trace, string search, string loki)
        => new((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/api/search", StringComparison.Ordinal))
            {
                return ScriptedStores.Json(search);
            }
            return path.Contains("/api/v2/traces/", StringComparison.Ordinal) ? ScriptedStores.Json(trace(path[(path.LastIndexOf('/') + 1)..])) : ScriptedStores.Json(loki);
        });

    public static ScriptedStores Failing(string kind)
        => new((_, _) => kind switch
        {
            "timeout" => throw new TaskCanceledException("The request timed out."),
            "notfound" => ScriptedStores.Json("{}", HttpStatusCode.NotFound),
            "malformed" => ScriptedStores.Json("{nope"),
            _ => ScriptedStores.Json("[]"),
        });
}
