using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.YouTube;

// YouTube Data API v3 over the named "youtube" HttpClient, at the addresses in YouTubeEndpoints. Only the request shapes are
// checked against Google's documentation; nothing here has been run against Google (see the Y2 report).
// Upload is the resumable protocol with one PUT of the whole body, streamed from the caller's stream.
public sealed class GoogleYouTubeClient(IHttpClientFactory clients, IOptions<YouTubeEndpoints> endpoints) : IYouTubeClient
{
    public const string Scope = "https://www.googleapis.com/auth/youtube.force-ssl";

    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UploadTimeout = TimeSpan.FromMinutes(30);

    private readonly YouTubeEndpoints _endpoints = endpoints.Value;

    public string AuthorizationUrl(YouTubeClientSettings client, string redirectUri, string state) =>
        _endpoints.Authorization + "?" + Form(
            ("client_id", client.ClientId),
            ("redirect_uri", redirectUri),
            ("response_type", "code"),
            ("scope", Scope),
            ("access_type", "offline"),
            ("prompt", "consent"),
            ("state", state));

    public async Task<YouTubeTokens> ExchangeCodeAsync(YouTubeClientSettings client, string code, string redirectUri, CancellationToken cancellationToken)
    {
        const string what = "connecting the channel";
        using var reply = await PostFormAsync(_endpoints.Token, what, cancellationToken,
            ("client_id", client.ClientId), ("client_secret", client.ClientSecret), ("code", code), ("grant_type", "authorization_code"), ("redirect_uri", redirectUri));
        var token = await ReadJsonAsync(reply, what, cancellationToken);
        var access = Text(token, "access_token");
        var refresh = Text(token, "refresh_token");
        if (access is null)
        {
            throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, "Google didn't give ShipLean an access token. Connect the channel again.");
        }

        if (refresh is null)
        {
            throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, "Google didn't give ShipLean a refresh token, so it couldn't keep access. Remove ShipLean in your Google account's third-party access list, then connect again.");
        }

        var scopes = (Text(token, "scope") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new YouTubeTokens(access, refresh, scopes);
    }

    public async Task RevokeAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken)
    {
        using var reply = await PostFormAsync(_endpoints.Revoke, "revoking access", cancellationToken, ("token", refreshToken));
    }

    public async Task<string> AccessTokenAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken)
    {
        const string what = "renewing access";
        using var reply = await PostFormAsync(_endpoints.Token, what, cancellationToken,
            ("client_id", client.ClientId), ("client_secret", client.ClientSecret), ("refresh_token", refreshToken), ("grant_type", "refresh_token"));
        return Text(await ReadJsonAsync(reply, what, cancellationToken), "access_token")
            ?? throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, "Google didn't give ShipLean an access token. Connect the channel again.");
    }

    public async Task<YouTubeChannel> ChannelAsync(string accessToken, CancellationToken cancellationToken)
    {
        const string what = "reading the channel";
        using var request = Authorized(HttpMethod.Get, $"{_endpoints.Api}/channels?part=snippet&mine=true", accessToken);
        using var reply = await SendAsync(request, what, CallTimeout, cancellationToken);
        var first = Items(await ReadJsonAsync(reply, what, cancellationToken)).FirstOrDefault();
        var id = Text(first, "id");
        var title = first.ValueKind == JsonValueKind.Object && first.TryGetProperty("snippet", out var snippet) ? Text(snippet, "title") : null;
        return id is null
            ? throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, "This Google account has no YouTube channel. Create one on YouTube, then connect again.")
            : new YouTubeChannel(id, title ?? id);
    }

    public async Task<string> UploadAsync(string accessToken, YouTubeUpload upload, Stream content, long length, CancellationToken cancellationToken)
    {
        const string what = "uploading the video";
        var session = await StartUploadAsync(accessToken, upload, length, what, cancellationToken);

        using var put = Authorized(HttpMethod.Put, session.AbsoluteUri, accessToken);
        put.Content = new StreamContent(content);
        put.Content.Headers.ContentLength = length;
        put.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(upload.ContentType);
        using var reply = await SendAsync(put, what, UploadTimeout, cancellationToken);
        return Text(await ReadJsonAsync(reply, what, cancellationToken), "id")
            ?? throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, "YouTube accepted the upload but gave no video id.");
    }

    public async Task<YouTubeVisibility> ReadPrivacyAsync(string accessToken, string videoId, CancellationToken cancellationToken)
    {
        const string what = "reading the video";
        using var request = Authorized(HttpMethod.Get, $"{_endpoints.Api}/videos?part=status&id={Uri.EscapeDataString(videoId)}", accessToken);
        using var reply = await SendAsync(request, what, CallTimeout, cancellationToken);
        var video = Items(await ReadJsonAsync(reply, what, cancellationToken)).FirstOrDefault();
        var privacy = video.ValueKind == JsonValueKind.Object && video.TryGetProperty("status", out var status) ? Text(status, "privacyStatus") : null;
        return privacy switch
        {
            "public" => YouTubeVisibility.Public,
            "unlisted" => YouTubeVisibility.Unlisted,
            "private" => YouTubeVisibility.Private,
            _ => throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, "YouTube has no such video, or didn't say who can watch it."),
        };
    }

    public async Task DeleteAsync(string accessToken, string videoId, CancellationToken cancellationToken)
    {
        using var request = Authorized(HttpMethod.Delete, $"{_endpoints.Api}/videos?id={Uri.EscapeDataString(videoId)}", accessToken);
        using var reply = await SendAsync(request, "deleting the video", CallTimeout, cancellationToken);
    }

    private async Task<Uri> StartUploadAsync(string accessToken, YouTubeUpload upload, long length, string what, CancellationToken cancellationToken)
    {
        using var start = Authorized(HttpMethod.Post, $"{_endpoints.Upload}/videos?uploadType=resumable&part=snippet,status", accessToken);
        start.Headers.Add("X-Upload-Content-Length", length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.Headers.Add("X-Upload-Content-Type", upload.ContentType);
        start.Content = JsonContent.Create(new
        {
            snippet = new { title = upload.Title, description = upload.Description },
            status = new { privacyStatus = upload.Visibility.ToString().ToLowerInvariant(), selfDeclaredMadeForKids = false },
        });
        using var reply = await SendAsync(start, what, CallTimeout, cancellationToken);

        // The session address comes from the reply; the bearer token goes only to the host the upload endpoint names.
        var location = reply.Headers.Location;
        var home = new Uri(_endpoints.Upload);
        if (location is null || !location.IsAbsoluteUri || location.Scheme != home.Scheme || location.Host != home.Host || location.Port != home.Port)
        {
            throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, "YouTube answered the upload with an address ShipLean won't send the video to.");
        }

        return location;
    }

    private async Task<HttpResponseMessage> PostFormAsync(string url, string what, CancellationToken cancellationToken, params (string Name, string Value)[] fields)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))),
        };
        return await SendAsync(request, what, CallTimeout, cancellationToken);
    }

    // Sends the request and returns the answer when it is a success. Any other answer, and any failure to get one, is a
    // YouTubeFailureException with fixed text; the caller's cancellation is not turned into one.
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string what, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        HttpResponseMessage reply;
        try
        {
            reply = await clients.CreateClient(YouTubeServices.HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw YouTubeErrors.Unreachable(what);
        }

        if (reply.IsSuccessStatusCode)
        {
            return reply;
        }

        using (reply)
        {
            throw await YouTubeErrors.FromAsync(reply, what, cancellationToken);
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage reply, string what, CancellationToken cancellationToken)
    {
        try
        {
            using var json = await JsonDocument.ParseAsync(await reply.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return json.RootElement.Clone();
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException)
        {
            throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, $"YouTube's answer made no sense while {what}.");
        }
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static IEnumerable<JsonElement> Items(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray()
            : [];

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    private static string Form(params (string Name, string Value)[] fields) =>
        string.Join('&', fields.Select(field => $"{field.Name}={Uri.EscapeDataString(field.Value)}"));
}
