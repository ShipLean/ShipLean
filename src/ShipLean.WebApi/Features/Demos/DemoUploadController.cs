using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.Application.Features.Demos.Upload;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.WorkItemDetails.Attachments;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Demos;

// An agent's `curl -F chapters=@... -F script=@... -F video=@... <url>`. Anonymous: the token in the query is the credential (no cookie, bearer or client
// header), and it is rate limited per IP. The token is in the query on purpose: the path reaches the request log, the query string does not.
[AllowAnonymous]
[EnableRateLimiting(UploadServices.RateLimit)]
[Route("api/uploads/demo")]
public sealed class DemoUploadController(IDemoUploads uploads) : ApiControllerBase
{
    [HttpPost]
    [StreamedUpload]
    [ProducesResponseType<DemoUploadedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<DemoUploadedDto> Upload([FromQuery] string? token, CancellationToken cancellationToken)
        => uploads.UploadAsync(token ?? string.Empty, Request, cancellationToken);
}
