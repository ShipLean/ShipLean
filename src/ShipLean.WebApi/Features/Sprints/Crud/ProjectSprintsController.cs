using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Sprints;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Sprints.Crud;

// The list is the shell's (GET api/projects/{id}/sprints); this adds the write.
[Route("api/projects/{id:guid}/sprints")]
public sealed class ProjectSprintsController(ISprintStore store, NextSprintDefaults defaults, IProjectAccess access) : ApiControllerBase
{
    [HttpGet("next")]
    [ProducesResponseType<SprintCreate>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<SprintCreate> Next(Guid id, [FromQuery] DateOnly? start, [FromQuery] DateOnly? end, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await defaults.GetAsync(id, start, end, cancellationToken);
    }

    [HttpPost]
    [ProducesResponseType<SprintDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<SprintDto> Create(Guid id, SprintCreate create, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await store.CreateAsync(id, create, null, cancellationToken);
    }
}
