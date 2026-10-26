using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Sprints.Capacity;

[Route("api/sprints/{id:guid}/capacity")]
public sealed class SprintCapacityController(ISprintStore store, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<SprintCapacityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<SprintCapacityDto> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Sprint, id, ProjectRole.Member, cancellationToken);
        return await store.GetCapacityAsync(id, cancellationToken);
    }

    [HttpPut]
    [ProducesResponseType<SprintCapacityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<SprintCapacityDto> Put(Guid id, SprintCapacityPut capacities, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Sprint, id, ProjectRole.Member, cancellationToken);
        return await store.PutCapacityAsync(id, capacities, cancellationToken);
    }
}
