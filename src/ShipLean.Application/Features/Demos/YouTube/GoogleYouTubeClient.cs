using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.YouTube;

public sealed class GoogleYouTubeClient : IYouTubeClient
{
    // TODO: the real client speaks to Google through the "youtube" HttpClient; nothing here calls a Google host.
    public string AuthorizationUrl(YouTubeClientSettings client, string redirectUri, string state) => throw new NotSupportedException();

    public Task<YouTubeTokens> ExchangeCodeAsync(YouTubeClientSettings client, string code, string redirectUri, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task RevokeAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<string> AccessTokenAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<YouTubeChannel> ChannelAsync(string accessToken, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<string> UploadAsync(string accessToken, YouTubeUpload upload, Stream content, long length, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<YouTubeVisibility> ReadPrivacyAsync(string accessToken, string videoId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task DeleteAsync(string accessToken, string videoId, CancellationToken cancellationToken) => throw new NotSupportedException();
}
