namespace ShipLean.Contracts.Demos;

public interface IYouTubeClient
{
    string AuthorizationUrl(YouTubeClientSettings client, string redirectUri, string state);

    Task<YouTubeTokens> ExchangeCodeAsync(YouTubeClientSettings client, string code, string redirectUri, CancellationToken cancellationToken);

    Task RevokeAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken);

    Task<string> AccessTokenAsync(YouTubeClientSettings client, string refreshToken, CancellationToken cancellationToken);

    Task<YouTubeChannel> ChannelAsync(string accessToken, CancellationToken cancellationToken);

    Task<string> UploadAsync(string accessToken, YouTubeUpload upload, Stream content, long length, CancellationToken cancellationToken);

    Task<YouTubeVisibility> ReadPrivacyAsync(string accessToken, string videoId, CancellationToken cancellationToken);

    Task DeleteAsync(string accessToken, string videoId, CancellationToken cancellationToken);
}
