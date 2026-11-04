using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.Application.Features.Demos.Brand;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.WorkItemDetails.Attachments;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Demos;

// An agent's `curl -o logo <url>` from the demo prompt. Anonymous: the token in the query is the credential (no cookie, bearer or client header),
// and it is rate limited per IP. Every token that is not live is the same 404. The literal segment wins over DownloadsController's {token}.
[AllowAnonymous]
[EnableRateLimiting(DownloadServices.RateLimit)]
[Route("api/downloads/demo-logo")]
public sealed class DemoLogoDownloadController(IDemoLogoDownload download) : ApiControllerBase
{
    [HttpGet]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download([FromQuery] string? token, CancellationToken cancellationToken)
    {
        var logo = await download.OpenAsync(token ?? string.Empty, Request, cancellationToken);
        return new DemoLogoFileResult(logo.Content, logo.ContentType, false);
    }
}
