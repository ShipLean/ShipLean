using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItemDetails.Attachments;

[Route("api/work-items/{id:guid}/attachments")]
public sealed class WorkItemAttachmentsController(AttachmentEndpoints endpoints) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AttachmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<AttachmentDto>> List(Guid id, CancellationToken cancellationToken)
        => endpoints.ListAsync(id, cancellationToken);

    // The file arrives as multipart form data in the "file" part. The action binds no form: it checks access, then streams the part into the store.
    [HttpPost]
    [StreamedUpload]
    [ProducesResponseType<AttachmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<AttachmentDto> Upload(Guid id, CancellationToken cancellationToken)
        => endpoints.UploadAsync(id, Request, cancellationToken);
}
