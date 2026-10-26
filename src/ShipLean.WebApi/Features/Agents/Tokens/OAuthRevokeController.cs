using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.WebApi.Features.Agents.Clients;
using ShipLean.Contracts.Agents;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Agents.Tokens;

// RFC 7009 revocation. Anonymous: the token is the credential. It answers 200 whether or not the token existed.
[AllowAnonymous]
[EnableRateLimiting(TokenServices.TokenRateLimit)]
[NoFormValueProviders]
[RequestSizeLimit(TokenServices.MaxBodyBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = TokenServices.MaxBodyBytes, ValueLengthLimit = TokenServices.MaxBodyBytes)]
[Route(AgentUrls.RevokePath)]
public sealed class OAuthRevokeController(TokenHandler handler) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<OAuthErrorDto>(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Revoke(CancellationToken cancellationToken) => handler.RevokeAsync(Request, cancellationToken);
}
