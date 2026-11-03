using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Retros;

[Route("api")]
public sealed class RetroDecisionsController(IRetroDecisionWrites writes, IProjectAccess access, ICurrentUser user) : ApiControllerBase
{
    [HttpPost("retros/points/{id:guid}/not-needed")]
    [ProducesResponseType<RetroPointDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<RetroPointDto> NotNeeded(Guid id, RetroNotNeededPut request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.RetroPoint, id, ProjectRole.Member, cancellationToken);
        RetroChecks.NotNeededPut(request);
        return await writes.MarkNotNeededAsync(id, request, new RetroActor(user.Id, null), cancellationToken);
    }

    [HttpPost("retros/points/{id:guid}/undo-not-needed")]
    [ProducesResponseType<RetroPointDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<RetroPointDto> UndoNotNeeded(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.RetroPoint, id, ProjectRole.Member, cancellationToken);
        return await writes.UndoNotNeededAsync(id, new RetroActor(user.Id, null), cancellationToken);
    }
}
