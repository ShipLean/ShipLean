using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics.PostHog;

// Sends batches to {host}/batch/. Never put the api key into a message or a log line: the dispatcher logs AnalyticsSendException.Message.
public sealed class PostHogSink(HttpClient http, ILogger<PostHogSink> logger) : IAnalyticsSink
{
    public const int MaxPersonCache = 10_000;

    // distinct id -> serialized person properties last sent, so $set goes on first sight and when the properties change.
    private readonly ConcurrentDictionary<string, string> _sentPersons = new();
    private int _quotaLogged;

    public AnalyticsProvider Provider => AnalyticsProvider.PostHog;

    public async Task SendAsync(IReadOnlyList<AnalyticsEvent> batch, AnalyticsTarget target, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        var pending = new Dictionary<string, string>();
        var withPerson = new HashSet<int>();
        for (var i = 0; i < batch.Count; i++)
        {
            var person = batch[i].Person;
            var signature = JsonSerializer.Serialize(person.Properties.OrderBy(p => p.Key, StringComparer.Ordinal).ToList());
            var known = pending.TryGetValue(person.DistinctId, out var last) || _sentPersons.TryGetValue(person.DistinctId, out last);
            if (!known || last != signature)
            {
                pending[person.DistinctId] = signature;
                withPerson.Add(i);
            }
        }

        var url = new Uri(target.Host.AbsoluteUri.TrimEnd('/') + "/batch/");
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(PostHogPayload.Build(target.SiteKey, batch, withPerson), Encoding.UTF8, "application/json"),
        };
        request.Headers.UserAgent.ParseAdd("ShipLean/" + target.ShipLeanVersion);

        string responseBody;
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new AnalyticsSendException($"PostHog answered {(int)response.StatusCode}.");
            }
            responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AnalyticsSendException("PostHog request failed.", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AnalyticsSendException("PostHog request timed out.", ex);
        }

        Remember(pending);
        WarnWhenQuotaLimited(responseBody);
    }

    private void Remember(Dictionary<string, string> sent)
    {
        foreach (var pair in sent)
        {
            if (_sentPersons.Count >= MaxPersonCache && !_sentPersons.ContainsKey(pair.Key))
            {
                _sentPersons.Clear();
            }
            _sentPersons[pair.Key] = pair.Value;
        }
    }

    private void WarnWhenQuotaLimited(string responseBody)
    {
        if (!IsQuotaLimited(responseBody) || Interlocked.Exchange(ref _quotaLogged, 1) == 1)
        {
            return;
        }
        logger.LogWarning("PostHog reports this project is quota limited; events may be dropped.");
    }

    private static bool IsQuotaLimited(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("quota_limited", out var quota))
            {
                return false;
            }
            return quota.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.Array => quota.GetArrayLength() > 0,
                _ => false,
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
