using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Reads;

public interface ISprintDemoReader
{
    Task<SprintDemoDto> GetAsync(Guid sprintId, CancellationToken cancellationToken = default);
}
