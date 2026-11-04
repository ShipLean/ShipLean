namespace ShipLean.Application.Features.Demos.Runs;

public interface ISprintDemoAutomaticRun
{
    Task StartAsync(Guid projectId, Guid sprintId, Guid closedBy, CancellationToken cancellationToken = default);
}
