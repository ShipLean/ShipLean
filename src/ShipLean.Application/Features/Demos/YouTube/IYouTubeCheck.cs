using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.YouTube;

public interface IYouTubeCheck
{
    Task<YouTubeProjectDto> CheckAsync(Guid projectId, YouTubeVisibility visibility, CancellationToken cancellationToken = default);
}
