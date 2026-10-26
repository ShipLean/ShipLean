using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.WebApi.Features.Agents.Clients;
using ShipLean.Contracts.Agents;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Agents.Tokens;

// Exchanges an authorization code or a refresh token for a token pair. Anonymous (the agent has no session) and rate
// limited per IP. Every failure is an OAuth error body, never an ApiError.
[AllowAnonymous]
[EnableRateLimiting(TokenServices.TokenRateLimit)]
[NoFormValueProviders]
[RequestSizeLimit(TokenServices.MaxBodyBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = TokenServices.MaxBodyBytes, ValueLengthLimit = TokenServices.MaxBodyBytes)]
[Route(AgentUrls.TokenPath)]
public sealed class OAuthTokenController(TokenHandler handler) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType<TokenResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<OAuthErrorDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<OAuthErrorDto>(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Token(CancellationToken cancellationToken) => handler.TokenAsync(Request, cancellationToken);
}
