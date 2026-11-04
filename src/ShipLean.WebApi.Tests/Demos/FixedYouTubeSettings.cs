using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Demos;

// IYouTubeSettings with the OAuth client the test set; null is an instance with YouTube off.
internal sealed class FixedYouTubeSettings : IYouTubeSettings
{
    public YouTubeClientSettings? Client { get; set; } = new("client-id", "client-secret");

    public Task<YouTubeClientSettings?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Client);
}
