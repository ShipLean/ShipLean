using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;

namespace ShipLean.Application.Features.Import.Jira;

// One HTTP exchange with a Jira site: the address check, the sign-in header, and the errors every call maps the same
// way (bad credentials are Validation, Jira unreachable or failing is NotConfigured). The token goes in the
// Authorization header only, never into a URL, a log line or an exception message.
public sealed class JiraTransport(IHttpClientFactory clients, IConfiguration configuration)
{
    public const string HttpClientName = "jira";

    // A JSON answer is read whole, so this is the most memory one call can take.
    public const int MaxBodyBytes = 16 * 1024 * 1024;

    private const int MaxRedirects = 5;
    private const int MessageLimit = 300;
    
    private const string RefusedMessage = "Jira refused the email and token. Check the email belongs to the account that created the token and that the token has not expired.";

    public bool AllowLocal => JiraEndpointPolicy.AllowsLocal(configuration);

    public Task<JiraReply> GetAsync(JiraCredentials credentials, string path, CancellationToken cancellationToken)
        => SendJsonAsync(credentials, HttpMethod.Get, path, null, cancellationToken);

    public Task<JiraReply> PostAsync(JiraCredentials credentials, string path, JsonNode body, CancellationToken cancellationToken)
        => SendJsonAsync(credentials, HttpMethod.Post, path, body, cancellationToken);

    // The response of a download. Redirects are followed by hand: the Authorization header goes only to the site's own origin (and only over https unless local
    // endpoints are allowed), never to wherever a redirect points (Jira Cloud answers 303 to Atlassian's media service, which has its own token in the URL).
    public async Task<HttpResponseMessage> OpenAsync(JiraCredentials credentials, Uri url, CancellationToken cancellationToken)
    {
        var site = JiraEndpointPolicy.Parse(credentials.BaseUrl, AllowLocal);
        var client = clients.CreateClient(HttpClientName);
        var current = url;
        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            var withToken = SameOrigin(site, current) && (current.Scheme == Uri.UriSchemeHttps || AllowLocal);
            var response = await SendAsync(client, site, credentials, current, HttpMethod.Get, null, withToken, cancellationToken);
            if (!IsRedirect(response.StatusCode))
            {
                return Checked(response, site);
            }
            current = Redirected(response, current);
            response.Dispose();
            JiraEndpointPolicy.Parse(current.GetLeftPart(UriPartial.Path), AllowLocal);
        }
        throw ApiException.NotConfigured($"Jira at {site} redirected too many times.");
    }

    // The body of a download, with the client's timeout as the deadline of every read.
    public Stream Bounded(HttpResponseMessage response, Stream body) => new JiraDeadlineStream(response, body, clients.CreateClient(HttpClientName).Timeout);

    public static bool SameOrigin(Uri site, Uri other)
        => site.Scheme == other.Scheme && string.Equals(site.Host, other.Host, StringComparison.OrdinalIgnoreCase) && site.Port == other.Port;

    private async Task<JiraReply> SendJsonAsync(JiraCredentials credentials, HttpMethod method, string path, JsonNode? body, CancellationToken cancellationToken)
    {
        var site = JiraEndpointPolicy.Parse(credentials.BaseUrl, AllowLocal);
        var url = new Uri(site.AbsoluteUri.TrimEnd('/') + path);
        var client = clients.CreateClient(HttpClientName);
        using var response = Checked(await SendAsync(client, site, credentials, url, method, body, true, cancellationToken), site);
        // HttpClient.Timeout stops at the headers, so the body gets the same deadline for the whole read.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(client.Timeout);
        string text;
        try
        {
            text = await ReadLimitedAsync(response.Content, site, deadline.Token);
        }
        catch (Exception error) when (error is HttpRequestException or IOException)
        {
            throw ApiException.NotConfigured($"Jira at {site} did not finish answering.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw ApiException.NotConfigured($"Jira at {site} took too long to send its answer.");
        }
        return Read(response.StatusCode, text, site);
    }

    // A site that never stops sending cannot fill the server's memory.
    private static async Task<string> ReadLimitedAsync(HttpContent content, Uri site, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                throw ApiException.Validation($"{site} sent a larger answer than ShipLean reads. Check the address.", "baseUrl");
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, Uri site, JiraCredentials credentials, Uri url, HttpMethod method, JsonNode? body, bool withToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (withToken)
        {
            request.Headers.Authorization = credentials.Kind == JiraKind.Cloud
                ? new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.Email}:{credentials.Token}")))
                : new AuthenticationHeaderValue("Bearer", credentials.Token);
        }
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }
        try
        {
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException error) when (error.InnerException is JiraBlockedAddressException)
        {
            throw ApiException.Validation("That Jira address is on a private or local network, which ShipLean does not connect to.", "baseUrl");
        }
        catch (HttpRequestException)
        {
            throw ApiException.NotConfigured($"Jira at {site} did not answer.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw ApiException.NotConfigured($"Jira at {site} took too long to answer.");
        }
    }

    // The cases every call treats alike. A redirect is never followed for a JSON call: it would carry the token off the site.
    private static HttpResponseMessage Checked(HttpResponseMessage response, Uri site)
    {
        var status = response.StatusCode;
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            response.Dispose();
            throw ApiException.Validation(RefusedMessage);
        }
        if ((int)status >= 500 || status is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout)
        {
            response.Dispose();
            throw ApiException.NotConfigured($"Jira at {site} is not answering properly (HTTP {(int)status}).");
        }
        if (IsRedirect(status))
        {
            response.Dispose();
            throw ApiException.Validation($"Jira at {site} redirected the request. Use the address the site really lives at (https, no sign-in page in between).", "baseUrl");
        }
        return response;
    }

    private static JiraReply Read(HttpStatusCode status, string text, Uri site)
    {
        JsonNode? json = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                json = JsonNode.Parse(text);
            }
            catch (JsonException)
            {
                if ((int)status is >= 200 and < 300)
                {
                    throw ApiException.Validation($"{site} did not answer like Jira. Check the address.", "baseUrl");
                }
            }
        }
        return new JiraReply(status, json, ErrorText(json));
    }

    // Jira Cloud and Server put the reason in errorMessages, errors{} or message.
    private static string? ErrorText(JsonNode? json)
    {
        if (json is not JsonObject body)
        {
            return null;
        }
        var parts = new List<string>();
        parts.AddRange((body["errorMessages"] as JsonArray ?? []).Select(m => (string?)m).OfType<string>());
        parts.AddRange((body["errors"] as JsonObject ?? []).Select(e => (string?)e.Value).OfType<string>());
        if ((string?)body["message"] is { } message)
        {
            parts.Add(message);
        }
        var text = string.Join(' ', parts);
        return text.Length == 0 ? null : text[..Math.Min(text.Length, MessageLimit)];
    }

    private static bool IsRedirect(HttpStatusCode status) => (int)status is 301 or 302 or 303 or 307 or 308;

    private static Uri Redirected(HttpResponseMessage response, Uri from)
        => response.Headers.Location is { } location ? new Uri(from, location) : throw ApiException.NotConfigured("Jira redirected a download without saying where.");
}
