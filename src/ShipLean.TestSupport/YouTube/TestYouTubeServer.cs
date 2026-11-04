using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts.Demos;

namespace ShipLean.TestSupport.YouTube;

// A stand-in for Google's OAuth and YouTube Data API v3 on Kestrel at http://127.0.0.1:<free port>: token, revoke, channels,
// the resumable upload, videos.list and videos.delete. Point GoogleYouTubeClient at it with services.Configure<YouTubeEndpoints>
// (Authorization, Token, Revoke, Api, Upload), and dispose it at teardown. It answers the request shapes in Google's
// documentation as the client under test was written to read them; it does not prove Google behaves the same.
public sealed class TestYouTubeServer : IAsyncDisposable
{
    public const string FullScope = "https://www.googleapis.com/auth/youtube.force-ssl";

    public const string NarrowScopeValue = "https://www.googleapis.com/auth/youtube.upload";

    public const string ChannelId = "UC-test-channel";

    public const string ChannelTitle = "Test channel";

    private static readonly string[] RecordedHeaders = ["Authorization", "Content-Type", "Content-Length", "X-Upload-Content-Length", "X-Upload-Content-Type"];

    private delegate Task<(string? Body, long Bytes, int Reads, long Largest)> Handler(HttpRequest request, HttpResponse response, CancellationToken ct);

    private readonly WebApplication _app;
    private readonly ConcurrentQueue<TestYouTubeCall> _calls = new();
    private readonly ConcurrentDictionary<string, bool> _refreshTokens = new();
    private readonly ConcurrentDictionary<string, string> _videos = new();
    private readonly ConcurrentDictionary<string, string> _sessions = new();
    private int _sequence;
    private long _uploadedBytes;

    private TestYouTubeServer(WebApplication app)
    {
        _app = app;
    }

    public string BaseUrl { get; private set; } = string.Empty;

    public string Authorization => $"{BaseUrl}/o/oauth2/v2/auth";

    public string Token => $"{BaseUrl}/token";

    public string Revoke => $"{BaseUrl}/revoke";

    public string Api => $"{BaseUrl}/youtube/v3";

    public string Upload => $"{BaseUrl}/upload/youtube/v3";

    // Every later upload gets this visibility, whatever the request said.
    public YouTubeVisibility? ForcedVisibility { get; set; }

    // Every later upload start answers 403 quotaExceeded.
    public bool QuotaExceeded { get; set; }

    // Every later token request answers 400 invalid_grant, its description quoting the refresh token the way a careless server might.
    public bool Revoked { get; set; }

    // The next token responses grant only youtube.upload.
    public bool NarrowScope { get; set; }

    // When set, the token responses name exactly this scope string (space-delimited, as Google sends it).
    public string? Scope { get; set; }

    // Revoke answers 400.
    public bool RevokeFails { get; set; }

    // The code exchange gives no refresh token (a second consent without prompt=consent does that).
    public bool OmitRefreshToken { get; set; }

    // The upload start answers a Location at this address instead of its own session address.
    public string? SessionLocationOverride { get; set; }

    // Bytes received by the upload PUTs so far; a test reads it from inside a source stream to see the body is streamed.
    public long UploadedBytes => Interlocked.Read(ref _uploadedBytes);

    public IReadOnlyList<TestYouTubeCall> Calls => [.. _calls];

    public static async Task<TestYouTubeServer> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var self = new TestYouTubeServer(app);
        self.Map(app);
        await app.StartAsync();
        self.BaseUrl = $"http://127.0.0.1:{new Uri(app.Urls.First()).Port}";
        return self;
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private void Map(WebApplication app)
    {
        app.MapPost("/token", async (HttpRequest request, HttpResponse response, CancellationToken ct) => await Record(request, response, ct, TokenAsync));
        app.MapPost("/revoke", async (HttpRequest request, HttpResponse response, CancellationToken ct) => await Record(request, response, ct, RevokeAsync));
        app.MapGet("/youtube/v3/channels", async (HttpRequest request, HttpResponse response, CancellationToken ct) => await Record(request, response, ct, ChannelsAsync));
        app.MapPost("/upload/youtube/v3/videos", async (HttpRequest request, HttpResponse response, CancellationToken ct) => await Record(request, response, ct, StartUploadAsync));
        app.MapPut("/upload/youtube/v3/videos", async (HttpRequest request, HttpResponse response, CancellationToken ct) => await Record(request, response, ct, PutUploadAsync));
        app.MapGet("/youtube/v3/videos", async (HttpRequest request, HttpResponse response, CancellationToken ct) => await Record(request, response, ct, ListVideoAsync));
        app.MapDelete("/youtube/v3/videos", async (HttpRequest request, HttpResponse response, CancellationToken ct) => await Record(request, response, ct, DeleteVideoAsync));
    }

    private async Task Record(HttpRequest request, HttpResponse response, CancellationToken ct, Handler handler)
    {
        var (body, bytes, reads, largest) = await handler(request, response, ct);
        _calls.Enqueue(new TestYouTubeCall(
            request.Method,
            request.Path.Value!,
            request.Query.ToDictionary(pair => pair.Key, pair => pair.Value.ToString()),
            RecordedHeaders.Where(name => request.Headers.ContainsKey(name)).ToDictionary(name => name, name => request.Headers[name].ToString()),
            body,
            bytes,
            reads,
            largest));
    }

    private async Task<(string?, long, int, long)> TokenAsync(HttpRequest request, HttpResponse response, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);
        var raw = string.Join('&', form.Select(pair => $"{pair.Key}={pair.Value}"));
        var grant = form["grant_type"].ToString();
        var refresh = form["refresh_token"].ToString();
        if (Revoked || (grant == "refresh_token" && !_refreshTokens.ContainsKey(refresh)))
        {
            await Json(response, ct, 400, new { error = "invalid_grant", error_description = $"Token {refresh} has been expired or revoked." });
            return (raw, 0, 0, 0);
        }

        var n = Interlocked.Increment(ref _sequence);
        var body = new Dictionary<string, object>
        {
            ["access_token"] = $"access-{n}",
            ["expires_in"] = 3599,
            ["scope"] = Scope ?? (NarrowScope ? NarrowScopeValue : FullScope),
            ["token_type"] = "Bearer",
        };
        if (grant == "authorization_code" && !OmitRefreshToken)
        {
            var issued = $"refresh-{n}";
            _refreshTokens[issued] = true;
            body["refresh_token"] = issued;
        }

        await Json(response, ct, 200, body);
        return (raw, 0, 0, 0);
    }

    private async Task<(string?, long, int, long)> RevokeAsync(HttpRequest request, HttpResponse response, CancellationToken ct)
    {
        var form = await request.ReadFormAsync(ct);
        var token = form["token"].ToString();
        if (RevokeFails)
        {
            await Json(response, ct, 400, new { error = "invalid_token", error_description = $"Token {token} could not be revoked." });
            return ($"token={token}", 0, 0, 0);
        }

        _refreshTokens.TryRemove(token, out _);
        response.StatusCode = 200;
        return ($"token={token}", 0, 0, 0);
    }

    private async Task<(string?, long, int, long)> ChannelsAsync(HttpRequest request, HttpResponse response, CancellationToken ct)
    {
        await Json(response, ct, 200, new { items = new[] { new { id = ChannelId, snippet = new { title = ChannelTitle } } } });
        return (null, 0, 0, 0);
    }

    private async Task<(string?, long, int, long)> StartUploadAsync(HttpRequest request, HttpResponse response, CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(ct);
        if (QuotaExceeded)
        {
            await Json(response, ct, 403, new { error = new { code = 403, message = "The request cannot be completed because you have exceeded your quota.", errors = new[] { new { reason = "quotaExceeded", domain = "youtube.quota" } } } });
            return (body, 0, 0, 0);
        }

        var id = Interlocked.Increment(ref _sequence);
        var requested = JsonDocument.Parse(body).RootElement.GetProperty("status").GetProperty("privacyStatus").GetString()!;
        _sessions[$"s{id}"] = requested;
        response.Headers.Location = SessionLocationOverride ?? $"{Upload}/videos?uploadType=resumable&upload_id=s{id}";
        response.StatusCode = 200;
        return (body, 0, 0, 0);
    }

    private async Task<(string?, long, int, long)> PutUploadAsync(HttpRequest request, HttpResponse response, CancellationToken ct)
    {
        var session = request.Query["upload_id"].ToString();
        if (!_sessions.TryRemove(session, out var requested))
        {
            await Json(response, ct, 404, new { error = new { code = 404, errors = new[] { new { reason = "notFound" } } } });
            return (null, 0, 0, 0);
        }

        var buffer = new byte[256 * 1024];
        long total = 0;
        long largest = 0;
        var reads = 0;
        int read;
        while ((read = await request.Body.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            reads++;
            largest = Math.Max(largest, read);
            Interlocked.Add(ref _uploadedBytes, read);
        }

        var videoId = $"video-{Interlocked.Increment(ref _sequence)}";
        var privacy = ForcedVisibility?.ToString().ToLowerInvariant() ?? requested;
        _videos[videoId] = privacy;
        await Json(response, ct, 201, new { id = videoId, status = new { privacyStatus = privacy } });
        return (null, total, reads, largest);
    }

    private async Task<(string?, long, int, long)> ListVideoAsync(HttpRequest request, HttpResponse response, CancellationToken ct)
    {
        var items = _videos.TryGetValue(request.Query["id"].ToString(), out var privacy)
            ? new[] { new { id = request.Query["id"].ToString(), status = new { privacyStatus = privacy } } }
            : [];
        await Json(response, ct, 200, new { items });
        return (null, 0, 0, 0);
    }

    private async Task<(string?, long, int, long)> DeleteVideoAsync(HttpRequest request, HttpResponse response, CancellationToken ct)
    {
        if (_videos.TryRemove(request.Query["id"].ToString(), out _))
        {
            response.StatusCode = 204;
        }
        else
        {
            await Json(response, ct, 404, new { error = new { code = 404, errors = new[] { new { reason = "videoNotFound" } } } });
        }

        return (null, 0, 0, 0);
    }

    private static async Task Json(HttpResponse response, CancellationToken ct, int status, object body)
    {
        response.StatusCode = status;
        response.ContentType = "application/json";
        await response.WriteAsync(JsonSerializer.Serialize(body), ct);
    }
}
