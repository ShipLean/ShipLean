using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.WorkItems.Endpoints;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItems;

[Route("api/work-items")]
public sealed class WorkItemStatusController(StatusConfirm status) : ApiControllerBase
{
    [HttpPost("{id:guid}/status/confirm")]
    [ProducesResponseType<WorkItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ConflictDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(Guid id, StatusConfirmRequest request, CancellationToken cancellationToken)
    {
        var (item, conflict) = await status.ConfirmAsync(id, request.Revision, cancellationToken);
        return conflict is null ? Ok(item) : Conflict(conflict);
    }
}
