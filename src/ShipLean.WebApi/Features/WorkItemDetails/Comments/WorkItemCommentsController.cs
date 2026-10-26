using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.WorkItemDetails.Comments;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItemDetails.Comments;

[Route("api/work-items/{id:guid}/comments")]
public sealed class WorkItemCommentsController(CommentEndpoints endpoints) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CommentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<CommentDto>> List(Guid id, CancellationToken cancellationToken)
        => endpoints.ListAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<CommentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<CommentDto> Add(Guid id, CommentCreate create, CancellationToken cancellationToken)
        => endpoints.AddAsync(id, create, cancellationToken);
}
