using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.WorkItemDetails.Comments;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItemDetails.Comments;

[Route("api/comments")]
public sealed class CommentsController(CommentEndpoints endpoints) : ApiControllerBase
{
    [HttpPatch("{id:guid}")]
    [ProducesResponseType<CommentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<CommentDto> Edit(Guid id, CommentEdit edit, CancellationToken cancellationToken)
        => endpoints.EditAsync(id, edit, cancellationToken);

    [HttpPost("{id:guid}/supersede")]
    [ProducesResponseType<CommentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<CommentDto> Supersede(Guid id, CancellationToken cancellationToken)
        => endpoints.SupersedeAsync(id, cancellationToken);

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Archive(Guid id, CancellationToken cancellationToken)
        => NoContentAfter(endpoints.ArchiveAsync(id, cancellationToken));
}
