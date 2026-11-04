namespace ShipLean.Contracts.Demos;

public interface ISprintDemos
{
    Task<SprintDemoDto> GetAsync(Guid sprintId, CancellationToken cancellationToken = default);

    Task<SprintDemoDto> CreateFromButtonAsync(Guid sprintId, string requestOrigin, CancellationToken cancellationToken = default);

    Task<SprintDemoHandover> CreateForAgentAsync(Guid sprintId, string baseUrl, string agentStamp, Guid? authorizationId, CancellationToken cancellationToken = default);

    Task StartAutomaticAsync(Guid projectId, Guid sprintId, Guid closedBy, CancellationToken cancellationToken = default);

    Task<SprintDemoDto> RetryYouTubeAsync(Guid sprintId, CancellationToken cancellationToken = default);
}
