using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Demos;

// The real ISprintDemos with StartAutomaticAsync noted when it finishes, or replaced by a throw while the recorder's Fail is set.
internal sealed class RecordingSprintDemos(ShipLean.Application.Features.Demos.SprintDemos inner, RecordedAutomaticRuns recorded) : ISprintDemos
{
    public Task<SprintDemoDto> GetAsync(Guid sprintId, CancellationToken cancellationToken = default) => inner.GetAsync(sprintId, cancellationToken);

    public Task<SprintDemoDto> CreateFromButtonAsync(Guid sprintId, string requestOrigin, CancellationToken cancellationToken = default)
        => inner.CreateFromButtonAsync(sprintId, requestOrigin, cancellationToken);

    public Task<SprintDemoHandover> CreateForAgentAsync(Guid sprintId, string baseUrl, string agentStamp, Guid? authorizationId, CancellationToken cancellationToken = default)
        => inner.CreateForAgentAsync(sprintId, baseUrl, agentStamp, authorizationId, cancellationToken);

    public async Task StartAutomaticAsync(Guid projectId, Guid sprintId, Guid closedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            if (recorded.Fail)
            {
                throw new InvalidOperationException("The automatic run failed.");
            }

            await inner.StartAutomaticAsync(projectId, sprintId, closedBy, cancellationToken);
        }
        finally
        {
            recorded.Finished(sprintId);
        }
    }

    public Task<SprintDemoDto> RetryYouTubeAsync(Guid sprintId, CancellationToken cancellationToken = default) => inner.RetryYouTubeAsync(sprintId, cancellationToken);
}
