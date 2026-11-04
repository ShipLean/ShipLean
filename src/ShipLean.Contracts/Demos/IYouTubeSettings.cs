namespace ShipLean.Contracts.Demos;

public interface IYouTubeSettings
{
    Task<YouTubeClientSettings?> GetAsync(CancellationToken cancellationToken = default);
}
