using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Sprints.Rollover;

[Route("api/sprints/{id:guid}/rollover")]
public sealed class SprintRolloverController(ISprintStore store, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<RolloverDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<RolloverDto> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Sprint, id, ProjectRole.Member, cancellationToken);
        return await store.GetRolloverAsync(id, cancellationToken);
    }

    [HttpPost]
    [ProducesResponseType<SprintDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<SprintDto> Apply(Guid id, RolloverRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Sprint, id, ProjectRole.Member, cancellationToken);
        return await store.RolloverAsync(id, request, cancellationToken);
    }
}
