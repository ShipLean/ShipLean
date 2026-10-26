using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItemDetails.Attachments;

[Route("api/attachments/{id:guid}/markdown")]
public sealed class AttachmentMarkdownController(MarkdownView view) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<AttachmentMarkdownDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<AttachmentMarkdownDto> View(Guid id, CancellationToken cancellationToken)
        => view.ViewAsync(id, cancellationToken);
}
