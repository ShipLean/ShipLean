using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Boards.View;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Boards.View;

[Route("api/boards/{id:guid}")]
public sealed class BoardViewController(BoardViews views) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<BoardViewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<BoardViewDto> Get(Guid id, [FromQuery] BoardViewQuery query, CancellationToken cancellationToken)
        => views.GetAsync(id, query, cancellationToken);

    [HttpPost("moves")]
    [ProducesResponseType<WriteResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ConflictDto>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Move(Guid id, BoardMoveRequest request, CancellationToken cancellationToken)
        => Answer(await views.MoveAsync(id, request, cancellationToken));

    // The store's conflict body is the sanctioned non-ApiError 409.
    private IActionResult Answer(WriteOutcome outcome) => outcome.Conflict is { } conflict ? Conflict(conflict) : Ok(outcome.Written);
}
