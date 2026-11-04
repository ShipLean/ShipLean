using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.YouTube;

public sealed class YouTubeCheck : IYouTubeCheck
{
    // TODO: the real check uploads the embedded test video. The stub is the 409 of a project with no channel, because the access sweep's allowed leg refuses 404.
    public Task<YouTubeProjectDto> CheckAsync(Guid projectId, YouTubeVisibility visibility, CancellationToken cancellationToken = default)
        => throw ApiException.Conflict("There is no YouTube channel to check.");
}
