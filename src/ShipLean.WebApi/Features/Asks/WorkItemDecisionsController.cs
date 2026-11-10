using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Asks.Decisions.Answer;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Asks;

[Route("api/work-items/{id:guid}/decisions")]
public sealed class WorkItemDecisionsController(DecisionEndpoints endpoints) : ApiControllerBase
{
    [HttpPost("confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<NoContentResult> Confirm(Guid id, ConfirmDecisionsRequest request, CancellationToken cancellationToken)
        => NoContentAfter(endpoints.ConfirmAsync(id, request, cancellationToken));

    [HttpPost("{number:int}/change")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<NoContentResult> Change(Guid id, int number, ChangeDecisionRequest request, CancellationToken cancellationToken)
        => NoContentAfter(endpoints.ChangeAsync(id, number, request, cancellationToken));

    [HttpPost("{number:int}/withdraw")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<NoContentResult> Withdraw(Guid id, int number, CancellationToken cancellationToken)
        => NoContentAfter(endpoints.WithdrawAsync(id, number, cancellationToken));

    [HttpPost("{number:int}/discuss")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Discuss(Guid id, int number, DiscussDecisionRequest request, CancellationToken cancellationToken)
        => NoContentAfter(endpoints.DiscussAsync(id, number, request, cancellationToken));
}
