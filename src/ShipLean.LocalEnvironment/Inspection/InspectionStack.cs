using System.Data.Common;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using ShipLean.Application.Features.Inspection;

namespace ShipLean.LocalEnvironment.Inspection;

// The Inspection stack the store contract suites share: an OpenTelemetry collector (contrib) writing traces and logs to ClickHouse,
// traces to Tempo and logs to Loki. One set of containers is started and reused across runs (like PostgresServer), on a fixed-name
// network, with a memory limit each because the host is shared. Tests keep apart by project key, never by new containers.
// scripts/inspection-stack.sh runs the same images and configs by docker compose.
public sealed class InspectionStack
{
    public const string CollectorImage = "otel/opentelemetry-collector-contrib:0.165.0";
    public const string ClickHouseImage = "clickhouse/clickhouse-server:26.3.63";
    public const string TempoImage = "grafana/tempo:2.10.10";
    public const string LokiImage = "grafana/loki:3.7.10";
    public const string ClickHouseUser = "shiplean";
    public const string ClickHouseSecret = "shiplean";

    private const long MiB = 1024 * 1024;
    private const string NetworkName = "shiplean-inspection-tests";
    private static readonly Lazy<Task<InspectionStack>> Shared = new(StartAsync);

    private InspectionStack(string collectorUrl, string clickHouseUrl, string tempoUrl, string lokiUrl)
        => (CollectorUrl, ClickHouseHttpUrl, TempoUrl, LokiUrl) = (collectorUrl, clickHouseUrl, tempoUrl, lokiUrl);

    // OTLP/HTTP base of the collector (post to /v1/traces and /v1/logs).
    public string CollectorUrl { get; }

    public string ClickHouseHttpUrl { get; }

    public string TempoUrl { get; }

    public string LokiUrl { get; }

    public static Task<InspectionStack> GetAsync() => Shared.Value;

    // The instance settings that point ShipLean at this stack: ClickHouse mode, or Tempo/Loki mode.
    public InspectionOptions Options(string store)
    {
        var http = new Uri(ClickHouseHttpUrl);
        return new InspectionOptions
        {
            Enabled = true,
            Store = store,
            AgentOtlpEndpoint = CollectorUrl,
            ClickHouseConnection = new DbConnectionStringBuilder
            {
                ["Host"] = http.Host,
                ["Port"] = http.Port,
                ["Protocol"] = http.Scheme,
                ["Username"] = ClickHouseUser,
                ["Password"] = ClickHouseSecret,
                ["Database"] = "otel",
            }.ConnectionString,
            TempoUrl = TempoUrl,
            LokiUrl = LokiUrl,
        };
    }

    // Waits until everything the fixture holds is queryable in all three stores, by polling their query APIs (never a fixed sleep):
    // raw rows in ClickHouse, spans by trace id in Tempo, log lines by session id in Loki. Returns what each store showed, or throws
    // a TimeoutException saying what it showed.
    public async Task<StoreCounts> WaitUntilVisibleAsync(LoadedFixture loaded, int spans, int logs, TimeSpan timeout, CancellationToken ct = default)
    {
        using var client = new HttpClient();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        var seen = "nothing yet";
        try
        {
            while (true)
            {
                var clickHouse = (spans: await ClickHouseCountAsync(client, "otel_traces", loaded, deadline.Token), logs: await ClickHouseCountAsync(client, "otel_logs", loaded, deadline.Token));
                var tempo = await TempoSpanCountAsync(client, loaded, deadline.Token);
                var loki = await LokiLineCountAsync(client, loaded, deadline.Token);
                seen = $"ClickHouse {clickHouse.spans} spans {clickHouse.logs} logs, Tempo {tempo} spans, Loki {loki} lines";
                if (clickHouse == (spans, logs) && tempo == spans && loki == logs && await TempoSearchesAsync(client, loaded, spans, deadline.Token))
                {
                    return new StoreCounts(clickHouse.spans, clickHouse.logs, tempo, loki);
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250), deadline.Token);
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"After {timeout.TotalSeconds:0}s the stores showed {seen}; expected {spans} spans and {logs} logs for project {loaded.ProjectKey}.");
        }
    }

    private async Task<int> ClickHouseCountAsync(HttpClient client, string table, LoadedFixture loaded, CancellationToken ct)
    {
        var query = $"SELECT count() FROM otel.{table} WHERE ResourceAttributes['shiplean.project'] = {{project:String}}";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ClickHouseHttpUrl}/?param_project={Uri.EscapeDataString(loaded.ProjectKey)}")
        {
            Content = new StringContent(query),
        };
        request.Headers.Add("X-ClickHouse-User", ClickHouseUser);
        request.Headers.Add("X-ClickHouse-Key", ClickHouseSecret);
        using var response = await client.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        // Until the exporter has created the schema the table is missing: that is "not there yet", any other failure is an error.
        if (!response.IsSuccessStatusCode)
        {
            return text.Contains("UNKNOWN_TABLE", StringComparison.Ordinal) || text.Contains("UNKNOWN_DATABASE", StringComparison.Ordinal)
                ? 0
                : throw new InvalidOperationException($"ClickHouse answered {(int)response.StatusCode}: {text}");
        }
        return int.Parse(text.Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }

    // Tempo's search index lags what trace-by-id serves by several seconds, so a finder that searches would see nothing yet. Ready when a
    // search by the project key names a trace (a fixture with no spans has nothing to search for).
    private async Task<bool> TempoSearchesAsync(HttpClient client, LoadedFixture loaded, int spans, CancellationToken ct)
    {
        if (spans == 0)
        {
            return true;
        }
        var query = Uri.EscapeDataString($"{{ resource.shiplean.project = \"{loaded.ProjectKey}\" }}");
        using var response = await client.GetAsync($"{TempoUrl}/api/search?q={query}&start={TimeProvider.System.GetUtcNow().AddHours(-4).ToUnixTimeSeconds()}&end={TimeProvider.System.GetUtcNow().AddMinutes(5).ToUnixTimeSeconds()}&limit=1", ct);
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))?["traces"]?.AsArray().Count > 0;
    }

    private async Task<int> TempoSpanCountAsync(HttpClient client, LoadedFixture loaded, CancellationToken ct)
    {
        var total = 0;
        foreach (var traceId in loaded.TraceIds.Values)
        {
            using var response = await client.GetAsync($"{TempoUrl}/api/traces/{traceId}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                continue;
            }
            response.EnsureSuccessStatusCode();
            total += CountSpans(JsonNode.Parse(await response.Content.ReadAsStringAsync(ct)));
        }
        return total;
    }

    private async Task<int> LokiLineCountAsync(HttpClient client, LoadedFixture loaded, CancellationToken ct)
    {
        var total = 0;
        var start = (loaded.StartedAt.AddHours(-1).UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100;
        var end = (loaded.StartedAt.AddHours(1).UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100;
        foreach (var sessionId in loaded.SessionIds.Values)
        {
            var query = $"{{service_name=~\".+\"}} | session_id=\"{sessionId}\"";
            var url = $"{LokiUrl}/loki/api/v1/query_range?limit=5000&start={start}&end={end}&query={Uri.EscapeDataString(query)}";
            using var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var result = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!["data"]!["result"]!.AsArray();
            total += result.Sum(stream => stream!["values"]!.AsArray().Count);
        }
        return total;
    }

    private static int CountSpans(JsonNode? node) => node switch
    {
        JsonObject obj => obj.Sum(p => p.Key == "spans" && p.Value is JsonArray spans ? spans.Count : CountSpans(p.Value)),
        JsonArray array => array.Sum(CountSpans),
        _ => 0,
    };

    private static async Task<InspectionStack> StartAsync()
    {
        var configs = Path.Combine(RepoLayout.Root, "src", "ShipLean.LocalEnvironment", "Inspection");
        var network = new NetworkBuilder().WithName(NetworkName).WithReuse(true).WithLabel("shiplean-tests", NetworkName).Build();
        await network.CreateAsync();

        var clickHouse = Base(ClickHouseImage, "clickhouse", network, 1024)
            .WithEnvironment("CLICKHOUSE_USER", ClickHouseUser)
            .WithEnvironment("CLICKHOUSE_PASSWORD", ClickHouseSecret)
            // Testcontainers leaves resource mappings out of the reuse hash: bump this whenever clickhouse-memory.xml changes, so hosts start a capped container.
            .WithEnvironment("SHIPLEAN_CLICKHOUSE_CONFIG", "memory-v1")
            .WithResourceMapping(new FileInfo(Path.Combine(configs, "clickhouse-memory.xml")), "/etc/clickhouse-server/config.d/")
            .WithPortBinding(8123, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8123).ForPath("/ping")))
            .Build();
        var tempo = Base(TempoImage, "tempo", network, 512)
            .WithResourceMapping(new FileInfo(Path.Combine(configs, "tempo.yaml")), "/etc/")
            .WithCommand("-config.file=/etc/tempo.yaml")
            .WithPortBinding(3200, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(3200).ForPath("/ready")))
            .Build();
        var loki = Base(LokiImage, "loki", network, 512)
            .WithResourceMapping(new FileInfo(Path.Combine(configs, "loki.yaml")), "/etc/")
            .WithCommand("-config.file=/etc/loki.yaml")
            .WithPortBinding(3100, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(3100).ForPath("/ready")))
            .Build();
        // Start the backends together, then the collector, which connects to all three at start.
        await Task.WhenAll(clickHouse.StartAsync(), tempo.StartAsync(), loki.StartAsync());

        var collector = Base(CollectorImage, "collector", network, 256)
            .WithResourceMapping(new FileInfo(Path.Combine(configs, "collector.yaml")), "/etc/")
            .WithCommand("--config=/etc/collector.yaml")
            .WithPortBinding(4318, true)
            .WithPortBinding(13133, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(13133)))
            .Build();
        await collector.StartAsync();

        return new InspectionStack(
            Url(collector, 4318), Url(clickHouse, 8123), Url(tempo, 3200), Url(loki, 3100));
    }

    private static ContainerBuilder Base(string image, string alias, INetwork network, long memoryMiB)
        => new ContainerBuilder(image)
            .WithReuse(true)
            .WithLabel("shiplean-tests", "inspection-" + alias)
            .WithNetwork(network)
            .WithNetworkAliases(alias)
            .WithCreateParameterModifier(p => (p.HostConfig ??= new()).Memory = memoryMiB * MiB);

    private static string Url(IContainer container, ushort port) => new UriBuilder(Uri.UriSchemeHttp, container.Hostname, container.GetMappedPublicPort(port)).Uri.ToString().TrimEnd('/');

}

// What each store held for one loaded fixture.
public sealed record StoreCounts(int ClickHouseSpans, int ClickHouseLogs, int TempoSpans, int LokiLines);
