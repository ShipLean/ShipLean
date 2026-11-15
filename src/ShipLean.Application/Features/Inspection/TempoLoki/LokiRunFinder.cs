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

    public async Task<IReadOnlyList<string>> SessionsAsync(RunQuery query, CancellationToken ct)
    {
        var scope = Scope(query);
        var item = $"{scope} | {Label(options.Fields.Item)}={QueryText.LogQlString(query.ItemKey)}";
        var pattern = QueryText.LogQlString(StoreRequests.ContainsPattern(query.ItemKey));
        var called = $"{scope} | tool_parameters=~{pattern} or tool_input=~{pattern} or input=~{pattern}";
        var sessions = new List<string>();
        foreach (var logQl in new[] { item, called })
        {
            var logs = await QueryAsync(logQl, query, DiscoveryLimit, ct);
            sessions.AddRange(logs.Select(l => l.SessionId).OfType<string>());
        }
        return [.. sessions.Distinct(StringComparer.Ordinal)];
    }

    public Task<IReadOnlyList<LogRecordData>> LogsAsync(RunQuery query, string sessionId, CancellationToken ct)
        => QueryAsync($"{Scope(query)} | {Label(options.Fields.Session)}={QueryText.LogQlString(sessionId)}", query, RunLimit, ct);

    // The base selector (and the project's extra matchers), then the project filter.
    private string Scope(RunQuery query)
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
            throw new ArgumentException("The project's Loki selector must be label matchers such as {team=\"platform\"}.", nameof(extra));
        }
        return options.LokiSelector.TrimEnd('}') + "," + extra.Trim().TrimStart('{');
    }

    private async Task<IReadOnlyList<LogRecordData>> QueryAsync(string logQl, RunQuery query, int limit, CancellationToken ct)
    {
        var url = $"loki/api/v1/query_range?direction=forward&limit={limit}&start={StoreRequests.UnixNanos(query.From)}&end={StoreRequests.UnixNanos(query.To) + 1}&query={Uri.EscapeDataString(logQl)}";
        using var document = await StoreRequests.GetJsonAsync(clients.CreateClient(InspectionHttp.LokiClient), url, query.TenantOverride, ct);
        var logs = new List<LogRecordData>();
        foreach (var stream in document!.RootElement.GetProperty("data").GetProperty("result").EnumerateArray())
        {
            var labels = Labels(stream.GetProperty("stream"));
            foreach (var value in stream.GetProperty("values").EnumerateArray())
            {
                logs.Add(Record(labels, value[0].GetString()!, value[1].GetString()!));
            }
        }
        return logs;
    }

    private LogRecordData Record(Dictionary<string, string> labels, string nanos, string line)
        => new(
            StoreRequests.FromNanos(long.Parse(nanos, System.Globalization.CultureInfo.InvariantCulture)),
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
        foreach (var label in stream.EnumerateObject())
        {
            labels[dotted.GetValueOrDefault(label.Name, label.Name)] = label.Value.GetString()!;
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
