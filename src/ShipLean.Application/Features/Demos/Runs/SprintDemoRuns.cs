using ShipLean.Application.Features.Demos.Brand;
using ShipLean.Application.Features.Demos.Reads;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Runs;

public sealed class SprintDemoRuns : ISprintDemoRuns
{
    // TODO: the real button run replaces this: the agent check, the demo item and the winning session.
    public Task<SprintDemoDto> CreateFromButtonAsync(Guid sprintId, string requestOrigin, CancellationToken cancellationToken = default)
        => Task.FromResult(SprintDemoReader.None(sprintId));

    // TODO: the real hand-over replaces this: the demo item, the upload link and the brand, decided in one transaction.
    public Task<SprintDemoHandover> CreateForAgentAsync(Guid sprintId, string baseUrl, string agentStamp, Guid? authorizationId, CancellationToken cancellationToken = default)
        => Task.FromResult(new SprintDemoHandover(
            sprintId,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            DateTimeOffset.MinValue,
            0,
            DemoBrandDefaults.Brand,
            string.Empty));
}
