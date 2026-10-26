using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Agents.Authorize;

// The browser leg of the authorization code flow. Anonymous, because an agent's browser has no session yet: the handler
// looks at the cookie itself and sends a signed-out browser to the sign-in page. Answers are redirects, except an unknown
// client, an unregistered redirect URI, or a protocol error for a non-loopback redirect: those are an ApiError (400) and
// never a redirect (RFC 6749 4.1.2.1, RFC 9700 4.11.2).
[AllowAnonymous]
[EnableRateLimiting(AuthorizeServices.RateLimit)]
[Route("oauth/authorize")]
public sealed class OAuthAuthorizeController(AuthorizeHandler handler) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Authorize([FromQuery] AuthorizeQuery query, CancellationToken cancellationToken)
        => handler.HandleAsync(query, User, Request.GetEncodedPathAndQuery(), cancellationToken);
}
