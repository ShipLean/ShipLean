using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Retros;

[Route("api")]
public sealed class RetroPointsController(IRetroPeopleWrites writes, IProjectAccess access) : ApiControllerBase
{
    [HttpPost("projects/{id:guid}/retros/points")]
    [ProducesResponseType<RetroPointDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(Guid id, RetroPointCreate request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        RetroChecks.PointCreate(request);
        return StatusCode(StatusCodes.Status201Created, await writes.AddPointAsync(id, request, cancellationToken));
    }

    [HttpPut("retros/points/{id:guid}/theme")]
    [ProducesResponseType<RetroPointDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<RetroPointDto> MoveTheme(Guid id, RetroPointThemePut request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.RetroPoint, id, ProjectRole.Member, cancellationToken);
        RetroChecks.ThemePut(request);
        return await writes.MoveThemeAsync(id, request, cancellationToken);
    }

    [HttpPost("retros/points/{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.RetroPoint, id, ProjectRole.Member, cancellationToken);
        return await NoContentAfter(writes.ArchiveAsync(id, cancellationToken));
    }

    [HttpPost("retros/points/{id:guid}/action")]
    [ProducesResponseType<RetroActionRefDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAction(Guid id, RetroActionCreate request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.RetroPoint, id, ProjectRole.Member, cancellationToken);
        RetroChecks.ActionCreate(request);
        return StatusCode(StatusCodes.Status201Created, await writes.CreateActionAsync(id, request, cancellationToken));
    }
}
