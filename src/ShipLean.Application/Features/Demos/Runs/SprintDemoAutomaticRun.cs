namespace ShipLean.Application.Features.Demos.Runs;

public sealed class SprintDemoAutomaticRun : ISprintDemoAutomaticRun
{
    // TODO: the real automatic run replaces this: claim the sprint, then start the session.
    public Task StartAsync(Guid projectId, Guid sprintId, Guid closedBy, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
