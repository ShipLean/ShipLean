using ShipLean.Application.Features.Demos.Reads;
using ShipLean.Application.Features.Demos.Runs;
using ShipLean.Application.Features.Demos.YouTube;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos;

// Only delegates, one collaborator per method, so the tasks that fill them in never edit the same file.
public sealed class SprintDemos(ISprintDemoReader reader, ISprintDemoRuns runs, ISprintDemoAutomaticRun automatic, IDemoYouTubeRetry retry) : ISprintDemos
{
    public Task<SprintDemoDto> GetAsync(Guid sprintId, CancellationToken cancellationToken = default)
        => reader.GetAsync(sprintId, cancellationToken);

    public Task<SprintDemoDto> CreateFromButtonAsync(Guid sprintId, string requestOrigin, CancellationToken cancellationToken = default)
        => runs.CreateFromButtonAsync(sprintId, requestOrigin, cancellationToken);

    public Task<SprintDemoHandover> CreateForAgentAsync(Guid sprintId, string baseUrl, string agentStamp, Guid? authorizationId, CancellationToken cancellationToken = default)
        => runs.CreateForAgentAsync(sprintId, baseUrl, agentStamp, authorizationId, cancellationToken);

    public Task StartAutomaticAsync(Guid projectId, Guid sprintId, Guid closedBy, CancellationToken cancellationToken = default)
        => automatic.StartAsync(projectId, sprintId, closedBy, cancellationToken);

    public Task<SprintDemoDto> RetryYouTubeAsync(Guid sprintId, CancellationToken cancellationToken = default)
        => retry.RetryAsync(sprintId, cancellationToken);
}
