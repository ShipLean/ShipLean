using System.Text;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics.PostHog;

// Sends web vitals readings to {host}/batch/ as $web_vitals events. Never put the api key into a message: the dispatcher logs AnalyticsSendException.Message.
public sealed class PostHogWebVitalsSink(IHttpClientFactory httpClientFactory) : IWebVitalsSink
{
    public AnalyticsProvider Provider => AnalyticsProvider.PostHog;

    public async Task SendAsync(IReadOnlyList<WebVitalsSample> batch, AnalyticsTarget target, CancellationToken cancellationToken)
    {
        var body = PostHogWebVitalsPayload.Build(target.SiteKey, batch, target.InstanceHost);
        if (body is null)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(target.Host.AbsoluteUri.TrimEnd('/') + "/batch/"))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.UserAgent.ParseAdd("ShipLean/" + target.ShipLeanVersion);

        try
        {
            using var client = httpClientFactory.CreateClient(AnalyticsServices.HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new AnalyticsSendException($"PostHog answered {(int)response.StatusCode} to web vitals.");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new AnalyticsSendException("PostHog web vitals request failed.", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AnalyticsSendException("PostHog web vitals request timed out.", ex);
        }
    }
}
