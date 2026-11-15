using Agoda.IoC.Core;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

// Does recent data carry each K1 field? Tempo: the tag values API (resource or span scope). Loki: the label names of recent log lines
// (stream labels and structured metadata both come back as the line's labels).
[RegisterSingleton]
public sealed class TempoLokiFieldCheck(IHttpClientFactory clients, InspectionOptions options, TimeProvider time)
{
    private const int RecentDays = 3;
    private const int SampleLines = 500;

    public async Task<IReadOnlyList<FieldCheck>> CheckAsync(CancellationToken ct)
    {
        var fields = options.Fields;
        var wanted = new[] { ("item", fields.Item), ("project", fields.Project), ("session", fields.Session), ("prompt", fields.Prompt), ("repo", fields.Repo), ("branch", fields.Branch), ("pr", fields.Pr) };
        var to = time.GetUtcNow();
        var from = to - TimeSpan.FromDays(Math.Min(RecentDays, options.MaxSearchDays));
        var lokiLabels = await LokiLabelsAsync(from, to, ct);
        var found = new List<FieldCheck>();
        foreach (var (name, attribute) in wanted)
        {
            var inLoki = lokiLabels.Contains(LokiRunFinder.Label(attribute));
            found.Add(new FieldCheck(name, attribute, inLoki || await TempoHasAsync(attribute, from, to, ct)));
        }
        return found;
    }

    private async Task<bool> TempoHasAsync(string attribute, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.TempoUrl))
        {
            return false;
        }
        foreach (var scope in new[] { "resource", "span" })
        {
            var url = $"api/v2/search/tag/{Uri.EscapeDataString(scope + "." + attribute)}/values?start={StoreRequests.UnixSeconds(from)}&end={StoreRequests.UnixSeconds(to)}";
            try
            {
                using var document = await StoreRequests.GetJsonAsync(clients.CreateClient(InspectionHttp.TempoClient), url, null, ct);
                if (document is not null && document.RootElement.TryGetProperty("tagValues", out var values) && values.GetArrayLength() > 0)
                {
                    return true;
                }
            }
            catch (HttpRequestException)
            {
                // An unreachable store has no field to show: the health check reports why.
                return false;
            }
        }
        return false;
    }

    private async Task<HashSet<string>> LokiLabelsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var labels = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(options.LokiUrl))
        {
            return labels;
        }
        var url = $"loki/api/v1/query_range?direction=backward&limit={SampleLines}&start={StoreRequests.UnixNanos(from)}&end={StoreRequests.UnixNanos(to)}&query={Uri.EscapeDataString(options.LokiSelector)}";
        try
        {
            using var document = await StoreRequests.GetJsonAsync(clients.CreateClient(InspectionHttp.LokiClient), url, null, ct);
            foreach (var stream in document!.RootElement.GetProperty("data").GetProperty("result").EnumerateArray())
            {
                labels.UnionWith(stream.GetProperty("stream").EnumerateObject().Select(p => p.Name));
            }
        }
        catch (HttpRequestException)
        {
            // Same as Tempo: unreachable means nothing found.
        }
        return labels;
    }
}
