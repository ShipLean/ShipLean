using ShipLean.Application.Fakes.YouTube;
using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Demos;

// The fake client whose first upload waits at the door until the test opens it, so a second runner can be started while the first is mid-upload.
// Any later upload goes straight through, and every arrival is counted.
internal sealed class GatedYouTubeClient : IYouTubeClient
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrivals;

    public FakeYouTubeClient Inner { get; } = new();

    public Task FirstUploadReached => _reached.Task;

    public int Arrivals => Volatile.Read(ref _arrivals);

    public void Open() => _open.TrySetResult();

    public string AuthorizationUrl(YouTubeClientSettings client, string redirectUri, string state) => Inner.AuthorizationUrl(client, redirectUri, state);

    public Task<YouTubeTokens> ExchangeCodeAsync(YouTubeClientSettings client, string code, string redirectUri, CancellationToken cancellationToken)
        => Inner.ExchangeCodeAsync(client, code, redirectUri, cancellationToken);

    public Task RevokeAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken) => Inner.RevokeAsync(client, refreshToken, cancellationToken);

    public Task<string> AccessTokenAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken) => Inner.AccessTokenAsync(client, refreshToken, cancellationToken);

    public Task<YouTubeChannel> ChannelAsync(string accessToken, CancellationToken cancellationToken) => Inner.ChannelAsync(accessToken, cancellationToken);

    public async Task<string> UploadAsync(string accessToken, YouTubeUpload upload, Stream content, long length, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _arrivals) == 1)
        {
            _reached.TrySetResult();
            await _open.Task;
        }

        return await Inner.UploadAsync(accessToken, upload, content, length, cancellationToken);
    }

    public Task<YouTubeVisibility> ReadPrivacyAsync(string accessToken, string videoId, CancellationToken cancellationToken)
        => Inner.ReadPrivacyAsync(accessToken, videoId, cancellationToken);

    public Task DeleteAsync(string accessToken, string videoId, CancellationToken cancellationToken) => Inner.DeleteAsync(accessToken, videoId, cancellationToken);
}
