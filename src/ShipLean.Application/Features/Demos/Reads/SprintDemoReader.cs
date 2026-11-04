using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Reads;

public sealed class SprintDemoReader : ISprintDemoReader
{
    // TODO: the real reader replaces this: the status rules over the sprint's columns, its attachments and its latest session.
    public Task<SprintDemoDto> GetAsync(Guid sprintId, CancellationToken cancellationToken = default)
        => Task.FromResult(None(sprintId));

    public static SprintDemoDto None(Guid sprintId)
        => new(
            sprintId,
            string.Empty,
            SprintDemoStatus.None,
            null,
            null,
            null,
            [],
            [],
            null,
            new SprintDemoYouTubeDto(YouTubeDemoState.None, null, null, null, null),
            new SprintDemoActionsDto(false, null, false));
}
