using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.WorkItemDetails.Attachments;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Mcp.Payloads;

// An agent's `curl -T file <url>` from create_payload_upload. Anonymous: the token in the URL is the credential (no cookie, bearer
// or client header), and it shares the per-IP rate limit of the attachment uploads. The literal "payloads" segment keeps it
// apart from attach_file's api/uploads/{token}.
[AllowAnonymous]
[EnableRateLimiting(UploadServices.RateLimit)]
[Route("api/uploads/payloads/{token}")]
public sealed class PayloadUploadsController(IToolPayloads payloads) : ApiControllerBase
{
    [HttpPut]
    [StreamedUpload]
    [ProducesResponseType<UploadedPayloadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<UploadedPayloadDto> Upload(string token, CancellationToken cancellationToken)
        => payloads.UploadAsync(token, Request.Body, cancellationToken);
}
