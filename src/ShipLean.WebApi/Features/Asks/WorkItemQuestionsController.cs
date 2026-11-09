using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Asks.Questions;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Asks;

[Route("api/work-items/{id:guid}/questions")]
public sealed class WorkItemQuestionsController(QuestionEndpoints endpoints) : ApiControllerBase
{
    [HttpPost("{number:int}/answer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<NoContentResult> Answer(Guid id, int number, AnswerQuestionRequest request, CancellationToken cancellationToken)
    {
        await endpoints.AnswerAsync(id, number, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{number:int}/withdraw")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<NoContentResult> Withdraw(Guid id, int number, CancellationToken cancellationToken)
    {
        await endpoints.WithdrawAsync(id, number, cancellationToken);
        return NoContent();
    }
}
