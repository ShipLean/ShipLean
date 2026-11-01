using System.Net.Http.Json;
using System.Text.Json;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics.Umami;

// Sends batches to Umami's /api/batch. Before an actor's first event in the process it sends one identify item.
public sealed class UmamiSink(IHttpClientFactory httpClientFactory) : IAnalyticsSink
{
    // Umami's /api/batch accepts at most 500 items per request.
    public const int MaxItemsPerRequest = 500;
    public const int MaxIdentifiedActors = 10_000;

    private readonly object _gate = new();
    private readonly HashSet<string> _identified = new(StringComparer.Ordinal);

    public AnalyticsProvider Provider => AnalyticsProvider.Umami;

    public async Task SendAsync(IReadOnlyList<AnalyticsEvent> batch, AnalyticsTarget target, CancellationToken cancellationToken)
    {
        // Identify keys are only remembered once their request succeeded, so a failed send is identified again on retry.
        var chunks = new List<(List<IReadOnlyDictionary<string, object?>> Items, List<string> NewKeys)>();
        var items = new List<IReadOnlyDictionary<string, object?>>();
        var keys = new List<string>();
        var planned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var analyticsEvent in batch)
        {
            var key = $"{target.SiteKey}|{analyticsEvent.Person.DistinctId}";
            var needsIdentify = !IsIdentified(key) && planned.Add(key);
            if (items.Count + (needsIdentify ? 2 : 1) > MaxItemsPerRequest)
            {
                chunks.Add((items, keys));
                items = [];
                keys = [];
            }
            if (needsIdentify)
            {
                items.Add(UmamiPayload.IdentifyItem(analyticsEvent.Person, target));
                keys.Add(key);
            }
            items.Add(UmamiPayload.EventItem(analyticsEvent, target));
        }
        if (items.Count > 0)
        {
            chunks.Add((items, keys));
        }

        foreach (var (chunkItems, newKeys) in chunks)
        {
            await PostAsync(chunkItems, target, cancellationToken);
            MarkIdentified(newKeys);
        }
    }

    private async Task PostAsync(List<IReadOnlyDictionary<string, object?>> items, AnalyticsTarget target, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(target.Host.ToString().TrimEnd('/') + "/api/batch")) { Content = JsonContent.Create(items) };
            request.Headers.TryAddWithoutValidation("User-Agent", $"ShipLean/{target.ShipLeanVersion}");
            using var client = httpClientFactory.CreateClient(AnalyticsServices.HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new AnalyticsSendException($"Umami at {target.Host.Host} answered {(int)response.StatusCode}.");
            }
            var errors = ErrorCount(body);
            if (errors > 0)
            {
                throw new AnalyticsSendException($"Umami at {target.Host.Host} reported {errors} errors in the batch.");
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new AnalyticsSendException($"Umami at {target.Host.Host} could not be reached ({e.GetType().Name}).", e);
        }
    }

    private static int ErrorCount(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Number
                    ? errors.GetInt32()
                    : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private bool IsIdentified(string key)
    {
        lock (_gate)
        {
            return _identified.Contains(key);
        }
    }

    private void MarkIdentified(List<string> keys)
    {
        lock (_gate)
        {
            foreach (var key in keys)
            {
                if (_identified.Count >= MaxIdentifiedActors)
                {
                    // Evicting everything is fine: an evicted actor is simply identified again.
                    _identified.Clear();
                }
                _identified.Add(key);
            }
        }
    }
}
