using System.Text.Json;
using System.Text.RegularExpressions;
using Agoda.IoC.Core;
using ShipLean.Application.Features.Inspection.Query;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

// The Loki half of finding runs (K3): LogQL searches for the log lines that name the item, the sessions they belong to, and the log
// records of a session. Loki keeps the OTLP attribute names with '.' as '_', in stream labels or structured metadata; both come back as
// the stream's labels. Every query has the stream selector, the project filter and a time window.
[RegisterSingleton]
public sealed partial class LokiRunFinder(IHttpClientFactory clients, InspectionOptions options)
{
    private const int DiscoveryLimit = 1000;
    private const int RunLimit = 5000;

    // The attributes whose Loki names are not the OTel names with '.' replaced the other way round: only the ones the rules read.
    private static readonly string[] KnownDotted = ["service.name", "user.email", "event.name", "shiplean.link_source", "host.name"];

    public async Task<IReadOnlyList<string>> SessionsAsync(RunQuery query, FindScope find, CancellationToken ct)
    {
        var scope = Scope(query);
        var item = $"{scope} | {Label(options.Fields.Item)}={QueryText.LogQlString(query.ItemKey)}";
        var pattern = QueryText.LogQlString(StoreRequests.ContainsPattern(query.ItemKey));
        var called = $"{scope} | tool_parameters=~{pattern} or tool_input=~{pattern} or input=~{pattern}";
        var sessions = new List<string>();
        foreach (var logQl in new[] { item, called })
        {
            var logs = await QueryAsync(logQl, query, DiscoveryLimit, ct);
            if (logs.Count >= DiscoveryLimit)
            {
                find.Note($"Loki returned {DiscoveryLimit} lines for one search, which is its limit; newer runs of this item may be missing.");
            }
            sessions.AddRange(logs.Select(l => l.SessionId).OfType<string>());
        }
        return [.. sessions.Distinct(StringComparer.Ordinal)];
    }

    // The session's log records in the project; null when there are more than the limit, so a summary would count only part of them.
    public async Task<IReadOnlyList<LogRecordData>?> LogsAsync(RunQuery query, string sessionId, FindScope scope, CancellationToken ct)
    {
        var logs = await QueryAsync($"{Scope(query)} | {Label(options.Fields.Session)}={QueryText.LogQlString(sessionId)}", query, RunLimit, ct);
        if (logs.Count >= RunLimit)
        {
            scope.Note($"Loki holds {RunLimit} or more log lines for a run of this item, its limit; that run is left out.");
            return null;
        }
        return [.. logs.Where(l => l.Attributes.GetValueOrDefault(options.Fields.Project) == query.ProjectKey)];
    }

    // The base selector (and the project's extra matchers), then the project filter.
    public string Scope(RunQuery query)
        => $"{Selector(query.SelectorOverride)} | {Label(options.Fields.Project)}={QueryText.LogQlString(query.ProjectKey)}";

    // The instance selector with the project's matchers added inside the braces. The override is settings text, not a value, so it must
    // be plain matchers (name, operator, quoted value) or it is refused.
    private string Selector(string? extra)
    {
        if (string.IsNullOrWhiteSpace(extra))
        {
            return options.LokiSelector;
        }
        if (!MatchersPattern().IsMatch(extra))
        {
            throw new LokiSelectorException("The project's Loki selector must be label matchers such as {team=\"platform\"}.");
        }
        return options.LokiSelector.TrimEnd('}') + "," + extra.Trim().TrimStart('{');
    }

    public async Task<IReadOnlyList<LogRecordData>> QueryAsync(string logQl, RunQuery query, int limit, CancellationToken ct)
    {
        var url = $"loki/api/v1/query_range?direction=forward&limit={limit}&start={StoreRequests.UnixNanos(query.From)}&end={StoreRequests.UnixNanos(query.To) + 1}&query={Uri.EscapeDataString(logQl)}";
        using var document = await StoreRequests.GetJsonAsync(clients.CreateClient(InspectionHttp.LokiClient), url, query.TenantOverride, ct)
            ?? throw new HttpRequestException("Loki answered 404 to query_range.");
        var logs = new List<LogRecordData>();
        foreach (var stream in StoreRequests.Items(StoreRequests.Member(StoreRequests.Member(document.RootElement, "data"), "result")))
        {
            var labels = Labels(StoreRequests.Member(stream, "stream"));
            foreach (var value in StoreRequests.Items(StoreRequests.Member(stream, "values")))
            {
                var pair = StoreRequests.Items(value).ToList();
                if (pair.Count < 2)
                {
                    throw new System.Text.Json.JsonException("A Loki entry is not a [time, line] pair.");
                }
                logs.Add(Record(labels, StoreRequests.Nanos(pair[0]), StoreRequests.Text(pair[1])));
            }
        }
        return logs;
    }

    private LogRecordData Record(Dictionary<string, string> labels, long nanos, string line)
        => new(
            StoreRequests.FromNanos(nanos),
            Severity(labels),
            line,
            labels,
            labels.GetValueOrDefault("trace_id"),
            labels.GetValueOrDefault("span_id"),
            labels.GetValueOrDefault(options.Fields.Session),
            labels.GetValueOrDefault(options.Fields.Prompt),
            labels.GetValueOrDefault("event.name"));

    // The OTel severity text in upper case, else derived from the OTel severity number (17 and above is an error).
    private static string Severity(Dictionary<string, string> labels)
    {
        if (labels.TryGetValue("severity_text", out var text) && text.Length > 0)
        {
            return text.ToUpperInvariant();
        }
        return labels.TryGetValue("severity_number", out var number) && int.TryParse(number, out var n) ? n switch { >= 21 => "FATAL", >= 17 => "ERROR", >= 13 => "WARN", >= 9 => "INFO", >= 5 => "DEBUG", _ => "TRACE" } : "INFO";
    }

    // Label names back to the OTel dotted form for the K1 fields and the few other names the rules read; any other name stays as Loki has it.
    private Dictionary<string, string> Labels(JsonElement stream)
    {
        var dotted = new[] { options.Fields.Item, options.Fields.Project, options.Fields.Session, options.Fields.Prompt, options.Fields.Repo, options.Fields.Branch, options.Fields.Pr }
            .Concat(KnownDotted)
            .ToDictionary(Label, name => name, StringComparer.Ordinal);
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var label in stream.ValueKind == JsonValueKind.Object ? stream.EnumerateObject() : throw new JsonException("A Loki stream has no labels."))
        {
            labels[dotted.GetValueOrDefault(label.Name, label.Name)] = StoreRequests.Text(label.Value);
        }
        return labels;
    }

    // The Loki label for an OTel attribute name: every character outside letters, digits and '_' becomes '_'.
    public static string Label(string attribute) => NonLabelCharacters().Replace(attribute, "_");

    [GeneratedRegex("[^A-Za-z0-9_]")]
    private static partial Regex NonLabelCharacters();

    [GeneratedRegex("^\\{(\\s*[A-Za-z_][A-Za-z0-9_]*\\s*(=|!=|=~|!~)\\s*\"([^\"\\\\]|\\\\.)*\"\\s*,?)+\\}\\z")]
    private static partial Regex MatchersPattern();
}
