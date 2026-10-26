using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Sprints.Crud;

[Route("api/sprints/{id:guid}")]
public sealed class SprintsController(ISprintStore store, IProjectAccess access) : ApiControllerBase
{
    [HttpPatch]
    [ProducesResponseType<SprintDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<SprintDto> Update(Guid id, SprintUpdate update, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Sprint, id, ProjectRole.Member, cancellationToken);
        return await store.UpdateAsync(id, update, cancellationToken);
    }
}
