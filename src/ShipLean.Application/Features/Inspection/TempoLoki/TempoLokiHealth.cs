using Agoda.IoC.Core;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

// Reachability and latency of the two stores, by their readiness endpoints. Latency is the slower of the two answers; there is no view lag in this mode.
[RegisterSingleton]
public sealed class TempoLokiHealth(IHttpClientFactory clients, InspectionOptions options, TimeProvider time)
{
    public async Task<StoreHealth> CheckAsync(CancellationToken ct)
    {
        var tempo = await PingAsync(InspectionHttp.TempoClient, "Tempo", options.TempoUrl, ct);
        var loki = await PingAsync(InspectionHttp.LokiClient, "Loki", options.LokiUrl, ct);
        var problems = new[] { tempo.Problem, loki.Problem }.OfType<string>().ToList();
        return new StoreHealth(problems.Count == 0, (int)Math.Max(tempo.Milliseconds, loki.Milliseconds), null, problems.Count == 0 ? null : string.Join(" ", problems));
    }

    private async Task<Ping> PingAsync(string client, string name, string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new Ping(0, $"The {name} URL is not set.");
        }
        var started = time.GetTimestamp();
        try
        {
            using var response = await clients.CreateClient(client).GetAsync("ready", ct);
            var elapsed = (long)time.GetElapsedTime(started).TotalMilliseconds;
            return new Ping(elapsed, response.IsSuccessStatusCode ? null : $"{name} answered {(int)response.StatusCode} to its readiness check.");
        }
        catch (HttpRequestException problem)
        {
            return new Ping(0, $"{name} is not reachable: {problem.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new Ping(0, $"{name} did not answer in time.");
        }
    }

    private sealed record Ping(long Milliseconds, string? Problem);
}
