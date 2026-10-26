using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Boards.View;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Boards.View;

// The children list a board card opens from a roll-up. It sits under the work item's route but belongs to the boards.
[Route("api/work-items/{id:guid}/children")]
public sealed class WorkItemChildrenController(BoardViews views) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ChildSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<ChildSummaryDto>> List(Guid id, [FromQuery] WorkItemTypeId? type, CancellationToken cancellationToken)
        => views.ChildrenAsync(id, type, cancellationToken);
}
