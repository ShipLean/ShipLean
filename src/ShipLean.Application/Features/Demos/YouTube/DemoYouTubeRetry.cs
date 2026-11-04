using ShipLean.Application.Features.Demos.Reads;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.YouTube;

public sealed class DemoYouTubeRetry(ISprintDemoReader reader) : IDemoYouTubeRetry
{
    // TODO: the real retry sets the failed upload back to pending (409 unless it failed); until then it answers the current read.
    public Task<SprintDemoDto> RetryAsync(Guid sprintId, CancellationToken cancellationToken = default)
        => reader.GetAsync(sprintId, cancellationToken);
}
