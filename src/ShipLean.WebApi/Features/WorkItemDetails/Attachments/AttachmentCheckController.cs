using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItemDetails.Attachments;

[Route("api/work-items/{id:guid}/attachments/check")]
public sealed class AttachmentCheckController(AttachmentCheck check) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<AttachmentCheckDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<AttachmentCheckDto> Check(Guid id, AttachmentCheckRequest request, CancellationToken cancellationToken)
        => check.CheckAsync(id, request, cancellationToken);
}
