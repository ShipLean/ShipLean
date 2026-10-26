using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItemDetails.Attachments;

[Route("api/attachments/{id:guid}")]
public sealed class AttachmentsController(AttachmentEndpoints endpoints) : ApiControllerBase
{
    [HttpGet("content")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var (attachment, content, inline) = await endpoints.DownloadAsync(id, cancellationToken);
        return new AttachmentFileResult(content.Content, content.ContentType, attachment.Name, inline);
    }

    [HttpPost("archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<NoContentResult> Archive(Guid id, CancellationToken cancellationToken)
        => NoContentAfter(endpoints.ArchiveAsync(id, cancellationToken));
}
