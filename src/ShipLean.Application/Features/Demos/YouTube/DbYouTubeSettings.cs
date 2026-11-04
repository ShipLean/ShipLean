using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.YouTube;

public sealed class DbYouTubeSettings : IYouTubeSettings
{
    // TODO: the real settings read the saved client, then SHIPLEAN_YOUTUBE_CLIENT_ID and _SECRET; until then YouTube is off.
    public Task<YouTubeClientSettings?> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<YouTubeClientSettings?>(null);
}
