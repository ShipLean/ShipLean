using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Taskboard;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Taskboard;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Taskboard;

[Route("api/sprints/{id:guid}/taskboard")]
public sealed class TaskboardController(TaskboardViews views, TaskboardCommands commands) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<TaskboardViewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<TaskboardViewDto> Get(Guid id, CancellationToken cancellationToken)
        => views.GetAsync(id, cancellationToken);

    [HttpPost("moves")]
    [ProducesResponseType<TaskboardMoveResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ConflictDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Move(Guid id, TaskboardMoveRequest request, CancellationToken cancellationToken)
        => Answer(await commands.MoveAsync(id, request, cancellationToken));

    // The store's conflict body is the sanctioned non-ApiError 409.
    private IActionResult Answer(TaskboardMoveOutcome outcome) => outcome.Conflict is { } conflict ? Conflict(conflict) : Ok(outcome.Written);
}
