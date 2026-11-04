using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.YouTube;

public interface IDemoYouTubeRetry
{
    Task<SprintDemoDto> RetryAsync(Guid sprintId, CancellationToken cancellationToken = default);
}
