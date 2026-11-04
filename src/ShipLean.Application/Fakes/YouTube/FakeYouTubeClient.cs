using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Fakes.YouTube;

// An in-memory IYouTubeClient with no I/O, held to YouTubeClientContract. Not registered by any module.
// It behaves as Google does where the contract says so: a channel can force a visibility whatever was chosen,
// a revoked grant stops every call, quota stops uploads, and a consent can come back narrower than asked.
public sealed class FakeYouTubeClient : IYouTubeClient
{
    public const string FullScope = "https://www.googleapis.com/auth/youtube.force-ssl";
    public const string NarrowScope = "https://www.googleapis.com/auth/youtube.upload";

    private readonly object _gate = new();
    private readonly HashSet<string> _liveRefreshTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, YouTubeVisibility> _videos = new(StringComparer.Ordinal);
    private readonly List<FakeYouTubeUpload> _uploads = [];
    private readonly List<string> _deletes = [];
    private readonly List<string> _revocations = [];
    private int _next;

    // The channel gives this visibility to every upload, whatever the upload chose. Null: the channel honours the choice.
    public YouTubeVisibility? ForcedVisibility { get; set; }

    // When true, the consent comes back without the full scope.
    public bool NarrowGrantedScope { get; set; }

    // When true, RevokeAsync fails and the refresh token stays live.
    public bool RevokeFails { get; set; }

    public YouTubeFailureKind? Failure { get; private set; }

    public IReadOnlyList<FakeYouTubeUpload> Uploads
    {
        get { lock (_gate) { return [.. _uploads]; } }
    }

    public IReadOnlyList<string> Deletes
    {
        get { lock (_gate) { return [.. _deletes]; } }
    }

    public IReadOnlyList<string> Revocations
    {
        get { lock (_gate) { return [.. _revocations]; } }
    }

    // Quota stops uploads only. Every other kind stops every call except RevokeAsync, which RevokeFails scripts.
    public void FailWith(YouTubeFailureKind? kind) => Failure = kind;

    public string AuthorizationUrl(YouTubeClientSettings client, string redirectUri, string state) =>
        "https://accounts.example.test/auth"
        + $"?client_id={Uri.EscapeDataString(client.ClientId)}"
        + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
        + "&response_type=code"
        + $"&scope={Uri.EscapeDataString(FullScope)}"
        + "&access_type=offline&prompt=consent"
        + $"&state={Uri.EscapeDataString(state)}";

    public Task<YouTubeTokens> ExchangeCodeAsync(YouTubeClientSettings client, string code, string redirectUri, CancellationToken cancellationToken)
    {
        ThrowIfFailing(uploading: false);
        string refresh;
        lock (_gate)
        {
            refresh = $"refresh-{++_next}";
            _liveRefreshTokens.Add(refresh);
        }

        IReadOnlyList<string> scopes = [NarrowGrantedScope ? NarrowScope : FullScope];
        return Task.FromResult(new YouTubeTokens($"access-{Interlocked.Increment(ref _next)}", refresh, scopes));
    }

    public Task RevokeAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken)
    {
        if (RevokeFails)
        {
            throw new YouTubeFailureException(YouTubeFailureKind.Network, "YouTube could not be reached to remove access.");
        }

        lock (_gate)
        {
            _liveRefreshTokens.Remove(refreshToken);
            _revocations.Add(refreshToken);
        }

        return Task.CompletedTask;
    }

    public Task<string> AccessTokenAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken)
    {
        ThrowIfFailing(uploading: false);
        lock (_gate)
        {
            if (!_liveRefreshTokens.Contains(refreshToken))
            {
                throw Revoked();
            }
        }

        return Task.FromResult($"access-{Interlocked.Increment(ref _next)}");
    }

    public Task<YouTubeChannel> ChannelAsync(string accessToken, CancellationToken cancellationToken)
    {
        ThrowIfFailing(uploading: false);
        return Task.FromResult(new YouTubeChannel("UC-fake-channel", "Fake channel"));
    }

    public async Task<string> UploadAsync(string accessToken, YouTubeUpload upload, Stream content, long length, CancellationToken cancellationToken)
    {
        ThrowIfFailing(uploading: true);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length != length)
        {
            throw new YouTubeFailureException(YouTubeFailureKind.Rejected, "YouTube refused the video because its size did not match.");
        }

        lock (_gate)
        {
            var id = $"video-{++_next}";
            _videos[id] = ForcedVisibility ?? upload.Visibility;
            _uploads.Add(new FakeYouTubeUpload(id, upload, buffer.ToArray()));
            return id;
        }
    }

    public Task<YouTubeVisibility> ReadPrivacyAsync(string accessToken, string videoId, CancellationToken cancellationToken)
    {
        ThrowIfFailing(uploading: false);
        lock (_gate)
        {
            return Task.FromResult(_videos.TryGetValue(videoId, out var visibility) ? visibility : throw NoVideo());
        }
    }

    public Task DeleteAsync(string accessToken, string videoId, CancellationToken cancellationToken)
    {
        ThrowIfFailing(uploading: false);
        lock (_gate)
        {
            if (!_videos.Remove(videoId))
            {
                throw NoVideo();
            }

            _deletes.Add(videoId);
        }

        return Task.CompletedTask;
    }

    private void ThrowIfFailing(bool uploading)
    {
        var kind = Failure;
        if (kind is null || (kind == YouTubeFailureKind.Quota && !uploading))
        {
            return;
        }

        throw kind.Value switch
        {
            YouTubeFailureKind.Quota => new YouTubeFailureException(kind.Value, "YouTube's daily upload quota for this project is used up. Try again tomorrow."),
            YouTubeFailureKind.Revoked => Revoked(),
            YouTubeFailureKind.Rejected => new YouTubeFailureException(kind.Value, "YouTube refused the request."),
            _ => new YouTubeFailureException(kind.Value, "YouTube could not be reached."),
        };
    }

    private static YouTubeFailureException Revoked() =>
        new(YouTubeFailureKind.Revoked, "Access to the YouTube channel was removed. Connect it again.");

    private static YouTubeFailureException NoVideo() =>
        new(YouTubeFailureKind.Rejected, "YouTube has no such video.");
}
