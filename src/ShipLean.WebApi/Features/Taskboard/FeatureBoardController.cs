using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Taskboard;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Taskboard;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Taskboard;

[Route("api/work-items/{featureId:guid}/feature-board")]
public sealed class FeatureBoardController(TaskboardCommands commands) : ApiControllerBase
{
    [HttpPost("moves")]
    [ProducesResponseType<TaskboardMoveResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ConflictDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Move(Guid featureId, TaskboardMoveRequest request, CancellationToken cancellationToken)
    {
        var outcome = await commands.MoveInFeatureAsync(featureId, request, cancellationToken);

        // The store's conflict body is the sanctioned non-ApiError 409.
        return outcome.Conflict is { } conflict ? Conflict(conflict) : Ok(outcome.Written);
    }
}
