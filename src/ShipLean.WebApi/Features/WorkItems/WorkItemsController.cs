using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.WorkItems.Endpoints;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItems;

[Route("api/work-items")]
public sealed class WorkItemsController(WorkItemViews views, WorkItemCommands commands) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<WorkItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<WorkItemDto> Get(Guid id, CancellationToken cancellationToken)
        => views.GetAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<WriteResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(WorkItemCreate create, CancellationToken cancellationToken)
        => Answer(await commands.CreateAsync(create, cancellationToken));

    [HttpPatch("{id:guid}")]
    [ProducesResponseType<WriteResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ConflictDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Patch(Guid id, WorkItemPatch patch, CancellationToken cancellationToken)
        => Answer(await commands.PatchAsync(id, patch, cancellationToken));

    [HttpPost("{id:guid}/reorder")]
    [ProducesResponseType<WriteResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ConflictDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reorder(Guid id, ReorderRequest request, CancellationToken cancellationToken)
        => Answer(await commands.ReorderAsync(id, request.ExpectedRevision, request.BeforeItemId, cancellationToken));

    [HttpPost("{id:guid}/remove")]
    [ProducesResponseType<WriteResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ConflictDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remove(Guid id, RemoveRequest request, CancellationToken cancellationToken)
        => Answer(await commands.RemoveAsync(id, request.ExpectedRevision, cancellationToken));

    // The store's conflict body is the sanctioned non-ApiError 409.
    private IActionResult Answer(WriteOutcome outcome) => outcome.Conflict is { } conflict ? Conflict(conflict) : Ok(outcome.Written);
}
