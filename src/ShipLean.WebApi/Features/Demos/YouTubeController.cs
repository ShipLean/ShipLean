using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Demos;

[Route("api")]
public sealed class YouTubeController(IYouTubeConnections connections, IProjectAccess access, IOptions<ForwardedHeadersOptions> forwarding) : ApiControllerBase
{
    [HttpPost("projects/{id:guid}/youtube/start")]
    [ProducesResponseType<YouTubeStartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<YouTubeStartDto> Start(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return await connections.StartAsync(id, RequestOrigin.Of(Request, forwarding.Value), cancellationToken);
    }

    // Google's browser comes back here, so the route is anonymous and the service checks the state, the signed-in user and the role itself. Always a redirect.
    [AllowAnonymous]
    [HttpGet("youtube/callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        var signedIn = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var user) ? user : (Guid?)null;
        return Redirect(await connections.CallbackAsync(signedIn, code, state, error, RequestOrigin.Of(Request, forwarding.Value), cancellationToken));
    }

    [HttpPost("projects/{id:guid}/youtube/check")]
    [ProducesResponseType<YouTubeProjectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<YouTubeProjectDto> Check(Guid id, YouTubeCheckRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return await connections.CheckAsync(id, request.Visibility, cancellationToken);
    }

    [HttpPost("projects/{id:guid}/youtube/disconnect")]
    [ProducesResponseType<YouTubeDisconnectDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<YouTubeDisconnectDto> Disconnect(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return await connections.DisconnectAsync(id, cancellationToken);
    }
}
