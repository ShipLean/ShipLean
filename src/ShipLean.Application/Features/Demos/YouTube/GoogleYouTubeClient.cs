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

    private delegate Task<T> Reader<T>(HttpResponseMessage reply, string what, CancellationToken cancellationToken);

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
        var token = await PostFormAsync(_endpoints.Token, what, ReadJsonAsync, cancellationToken,
            ("client_id", client.ClientId), ("client_secret", client.ClientSecret), ("code", code), ("grant_type", "authorization_code"), ("redirect_uri", redirectUri));
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
        await PostFormAsync(_endpoints.Revoke, "revoking access", Done, cancellationToken, ("token", refreshToken));
    }

    public async Task<string> AccessTokenAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken)
    {
        const string what = "renewing access";
        var token = await PostFormAsync(_endpoints.Token, what, ReadJsonAsync, cancellationToken,
            ("client_id", client.ClientId), ("client_secret", client.ClientSecret), ("refresh_token", refreshToken), ("grant_type", "refresh_token"));
        return Text(token, "access_token")
            ?? throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, "Google didn't give ShipLean an access token. Connect the channel again.");
    }

    public async Task<YouTubeChannel> ChannelAsync(string accessToken, CancellationToken cancellationToken)
    {
        const string what = "reading the channel";
        using var request = Authorized(HttpMethod.Get, $"{_endpoints.Api}/channels?part=snippet&mine=true", accessToken);
        var first = Items(await SendAsync(request, what, CallTimeout, ReadJsonAsync, cancellationToken)).FirstOrDefault();
        var id = Text(first, "id")
            ?? throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, "This Google account has no YouTube channel. Create one on YouTube, then connect again.");
        var title = first.TryGetProperty("snippet", out var snippet) ? Text(snippet, "title") : null;
        return new YouTubeChannel(id, title
            ?? throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, "YouTube gave the channel no title. Connect the channel again."));
    }

    public async Task<string> UploadAsync(string accessToken, YouTubeUpload upload, Stream content, long length, CancellationToken cancellationToken)
    {
        const string what = "uploading the video";
        var session = await StartUploadAsync(accessToken, upload, length, what, cancellationToken);

        using var put = Authorized(HttpMethod.Put, session.AbsoluteUri, accessToken);
        put.Content = new StreamContent(content);
        put.Content.Headers.ContentLength = length;
        put.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(upload.ContentType);
        return Text(await SendAsync(put, what, UploadTimeout, ReadJsonAsync, cancellationToken), "id")
            ?? throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, "YouTube accepted the upload but gave no video id.");
    }

    public async Task<YouTubeVisibility> ReadPrivacyAsync(string accessToken, string videoId, CancellationToken cancellationToken)
    {
        const string what = "reading the video";
        using var request = Authorized(HttpMethod.Get, $"{_endpoints.Api}/videos?part=status&id={Uri.EscapeDataString(videoId)}", accessToken);
        var video = Items(await SendAsync(request, what, CallTimeout, ReadJsonAsync, cancellationToken)).FirstOrDefault();
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
        await SendAsync(request, "deleting the video", CallTimeout, Done, cancellationToken);
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
        return await SendAsync(start, what, CallTimeout, SessionAddress, cancellationToken);
    }

    // The session address comes from the reply; the bearer token goes only to the host the upload endpoint names.
    private Task<Uri> SessionAddress(HttpResponseMessage reply, string what, CancellationToken cancellationToken)
    {
        var location = reply.Headers.Location;
        var home = new Uri(_endpoints.Upload);
        return location is null || !location.IsAbsoluteUri || location.Scheme != home.Scheme || location.Host != home.Host || location.Port != home.Port
            ? throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, "YouTube answered the upload with an address ShipLean won't send the video to.")
            : Task.FromResult(location);
    }

    private async Task<T> PostFormAsync<T>(string url, string what, Reader<T> read, CancellationToken cancellationToken, params (string Name, string Value)[] fields)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))),
        };
        return await SendAsync(request, what, CallTimeout, read, cancellationToken);
    }

    // Sends the request, then reads the answer with `read`, all under one time limit. A success is returned as `read` made it. Any
    // other answer, and any failure to get or read one (a refused connection, a drop or a stall mid-body, the limit), is a
    // YouTubeFailureException with fixed text; the caller's own cancellation is not turned into one.
    private async Task<T> SendAsync<T>(HttpRequestMessage request, string what, TimeSpan timeout, Reader<T> read, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            using var reply = await clients.CreateClient(YouTubeServices.HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token);
            if (!reply.IsSuccessStatusCode)
            {
                throw await YouTubeErrors.FromAsync(reply, what, limit.Token);
            }

            return await read(reply, what, limit.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw YouTubeErrors.Unreachable(what);
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage reply, string what, CancellationToken cancellationToken)
    {
        try
        {
            using var json = await JsonDocument.ParseAsync(await reply.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return json.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw YouTubeErrors.Failure(YouTubeFailureKind.Rejected, $"YouTube's answer made no sense while {what}.");
        }
    }

    private static Task<bool> Done(HttpResponseMessage reply, string what, CancellationToken cancellationToken) => Task.FromResult(true);

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
