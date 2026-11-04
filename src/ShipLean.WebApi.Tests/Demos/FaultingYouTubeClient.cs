using ShipLean.Application.Fakes.YouTube;
using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Demos;

// The fake client with two calls that can be made to fail on their own, which the fake cannot do (its failures stop every call). Nothing else changes.
internal sealed class FaultingYouTubeClient : IYouTubeClient
{
    public FakeYouTubeClient Inner { get; } = new();

    // Thrown by ReadPrivacyAsync when set.
    public Exception? ReadBackThrows { get; set; }

    // Thrown by DeleteAsync when set; the video then stays in the fake.
    public Exception? DeleteThrows { get; set; }

    public string AuthorizationUrl(YouTubeClientSettings client, string redirectUri, string state) => Inner.AuthorizationUrl(client, redirectUri, state);

    public Task<YouTubeTokens> ExchangeCodeAsync(YouTubeClientSettings client, string code, string redirectUri, CancellationToken cancellationToken)
        => Inner.ExchangeCodeAsync(client, code, redirectUri, cancellationToken);

    public Task RevokeAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken) => Inner.RevokeAsync(client, refreshToken, cancellationToken);

    public Task<string> AccessTokenAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken) => Inner.AccessTokenAsync(client, refreshToken, cancellationToken);

    public Task<YouTubeChannel> ChannelAsync(string accessToken, CancellationToken cancellationToken) => Inner.ChannelAsync(accessToken, cancellationToken);

    public Task<string> UploadAsync(string accessToken, YouTubeUpload upload, Stream content, long length, CancellationToken cancellationToken)
        => Inner.UploadAsync(accessToken, upload, content, length, cancellationToken);

    public Task<YouTubeVisibility> ReadPrivacyAsync(string accessToken, string videoId, CancellationToken cancellationToken)
        => ReadBackThrows is { } failure ? Task.FromException<YouTubeVisibility>(failure) : Inner.ReadPrivacyAsync(accessToken, videoId, cancellationToken);

    public Task DeleteAsync(string accessToken, string videoId, CancellationToken cancellationToken)
        => DeleteThrows is { } failure ? Task.FromException(failure) : Inner.DeleteAsync(accessToken, videoId, cancellationToken);
}
