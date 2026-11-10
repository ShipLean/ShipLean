using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Inbox;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inbox;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Inbox;

// The caller's own mentions: IMentionWrites checks the mention is theirs and that they are a Member of its project.
[Route("api/mentions/{id:guid}")]
public sealed class MentionsController(IMentionWrites mentions) : ApiControllerBase
{
    [HttpPost("seen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Seen(Guid id, CancellationToken cancellationToken)
        => NoContentAfter(mentions.SeenAsync(id, cancellationToken));

    [HttpPost("reply")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<NoContentResult> Reply(Guid id, MentionReplyRequest request, CancellationToken cancellationToken)
        => NoContentAfter(mentions.ReplyAsync(id, request.Body, cancellationToken));
}
