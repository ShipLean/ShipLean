using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Data;

public sealed class SprintDemoDataReader : ISprintDemoData
{
    // TODO: the real reader replaces this: the sprint's features, stories, retro and actions from the stores.
    public Task<SprintDemoData> ReadAsync(Guid sprintId, CancellationToken cancellationToken = default)
        => Task.FromResult(new SprintDemoData(
            new SprintDemoSprint("Sprint", string.Empty, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 14), true),
            new SprintDemoProject("KEY", "Project", null, null, string.Empty),
            [],
            [],
            new SprintDemoRetroData([], [], [], []),
            [],
            false));
}
