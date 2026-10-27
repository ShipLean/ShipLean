using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItemDetails.Attachments;

// An agent's `curl -T file <url>` from attach_file. Anonymous: the token in the URL is the credential (no cookie, bearer or
// client header), and it is rate limited per IP.
[AllowAnonymous]
[EnableRateLimiting(UploadServices.RateLimit)]
[Route("api/uploads/{token}")]
public sealed class UploadsController(AttachmentUploads handler) : ApiControllerBase
{
    [HttpPut]
    [StreamedUpload]
    [ProducesResponseType<UploadedAttachmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<UploadedAttachmentDto> Upload(string token, CancellationToken cancellationToken)
        => handler.UploadAsync(token, Request, cancellationToken);
}
