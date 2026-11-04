using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Runs;

public interface ISprintDemoRuns
{
    Task<SprintDemoDto> CreateFromButtonAsync(Guid sprintId, string requestOrigin, CancellationToken cancellationToken = default);

    Task<SprintDemoHandover> CreateForAgentAsync(Guid sprintId, string baseUrl, string agentStamp, Guid? authorizationId, CancellationToken cancellationToken = default);
}
