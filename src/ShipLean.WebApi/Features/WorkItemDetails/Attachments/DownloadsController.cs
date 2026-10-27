using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItemDetails.Attachments;

// An agent's `curl -o file <url>` from read_attachment. Anonymous: the token in the URL is the credential (no cookie, bearer or
// client header), and it is rate limited per IP. The bytes and headers are the normal attachment download's.
[AllowAnonymous]
[EnableRateLimiting(DownloadServices.RateLimit)]
[Route("api/downloads/{token}")]
public sealed class DownloadsController(AttachmentDownloads handler) : ApiControllerBase
{
    [HttpGet]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(string token, CancellationToken cancellationToken)
    {
        var (attachment, content, inline) = await handler.DownloadAsync(token, Request, cancellationToken);
        return new AttachmentFileResult(content.Content, content.ContentType, attachment.Name, inline);
    }
}
