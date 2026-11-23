using System.Net.Http.Json;
using System.Text.Json;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics.Umami;

// Sends web vitals readings to Umami as performance items, one item per sample, at most 500 per request.
public sealed class UmamiWebVitalsSink(IHttpClientFactory httpClientFactory) : IWebVitalsSink
{
    public AnalyticsProvider Provider => AnalyticsProvider.Umami;

    public async Task SendAsync(IReadOnlyList<WebVitalsSample> batch, AnalyticsTarget target, CancellationToken cancellationToken)
    {
        foreach (var chunk in batch.Chunk(UmamiSink.MaxItemsPerRequest))
        {
            var items = chunk.Select(sample => UmamiWebVitalsPayload.PerformanceItem(sample, target)).ToList();
            await PostAsync(items, target, cancellationToken);
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
                throw new AnalyticsSendException($"Umami at {target.Host.Host} answered {(int)response.StatusCode} to web vitals.");
            }
            var errors = ErrorCount(body);
            if (errors > 0)
            {
                throw new AnalyticsSendException($"Umami at {target.Host.Host} reported {errors} errors in the web vitals batch.");
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
}
