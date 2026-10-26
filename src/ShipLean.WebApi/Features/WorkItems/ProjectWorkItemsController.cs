using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.WorkItems.Endpoints;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItems;

[Route("api/projects/{id:guid}")]
public sealed class ProjectWorkItemsController(WorkItemViews views) : ApiControllerBase
{
    [HttpGet("work-items")]
    [ProducesResponseType<IReadOnlyList<WorkItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<WorkItemDto>> List(Guid id, [FromQuery] WorkItemTypeId? type, CancellationToken cancellationToken)
        => views.ListAsync(id, type, cancellationToken);

    [HttpGet("work-items/by-key/{itemKey}")]
    [ProducesResponseType<WorkItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<WorkItemDto> ByKey(Guid id, string itemKey, CancellationToken cancellationToken)
        => views.ByKeyAsync(id, itemKey, cancellationToken);

    [HttpGet("tags")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<string>> Tags(Guid id, CancellationToken cancellationToken)
        => views.TagsAsync(id, cancellationToken);
}
