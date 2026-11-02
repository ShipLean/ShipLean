using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Retros;

// Action items over retro points: one item can cover many points.
[Route("api")]
public sealed class RetroActionsController(IRetroDecisionWrites writes, IProjectAccess access, ICurrentUser user) : ApiControllerBase
{
    [HttpPost("projects/{id:guid}/retros/actions")]
    [ProducesResponseType<RetroActionDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(Guid id, RetroActionNew request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        RetroChecks.ActionNew(request);
        return StatusCode(StatusCodes.Status201Created, await writes.CreateActionAsync(id, request, new RetroActor(user.Id, null), cancellationToken));
    }

    [HttpPost("retros/actions/{workItemId:guid}/points")]
    [ProducesResponseType<RetroActionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<RetroActionDetailDto> AddPoints(Guid workItemId, RetroActionAddPoints request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        RetroChecks.ActionAddPoints(request);
        return await writes.AddPointsAsync(workItemId, request, new RetroActor(user.Id, null), cancellationToken);
    }

    [HttpPut("retros/actions/{workItemId:guid}")]
    [ProducesResponseType<RetroActionDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<RetroActionDetailDto> Update(Guid workItemId, RetroActionPut request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        RetroChecks.ActionPut(request);
        return await writes.UpdateActionAsync(workItemId, request, new RetroActor(user.Id, null), cancellationToken);
    }
}
