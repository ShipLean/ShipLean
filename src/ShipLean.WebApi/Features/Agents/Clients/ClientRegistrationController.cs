using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.Contracts.Agents;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Agents.Clients;

// Dynamic client registration (RFC 7591). Anonymous and rate limited per IP. It answers in OAuth's error format, so the
// body is read by hand: the framework's own 400 for a malformed body would be the wrong shape.
[AllowAnonymous]
[EnableRateLimiting(ClientsServices.RegisterRateLimit)]
[Route("oauth/register")]
public sealed class ClientRegistrationController(IOAuthClients clients, TimeProvider clock) : ApiControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<ClientRegistrationResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<OAuthErrorDto>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var body = await ReadBodyAsync(cancellationToken);
        var (registration, error) = body is null
            ? (null, new OAuthErrorDto("invalid_client_metadata", "The body is missing or too large."))
            : ClientRegistrationParser.Parse(body);
        if (registration is null)
        {
            return BadRequest(error);
        }

        var client = await clients.RegisterAsync(registration, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new ClientRegistrationResponseDto(
            client.ClientId, clock.GetUtcNow().ToUnixTimeSeconds(), client.ClientName, client.RedirectUris, "none",
            ["authorization_code", "refresh_token"], ["code"]));
    }

    private async Task<byte[]?> ReadBodyAsync(CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await Request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > ClientRegistrationParser.MaxBodyBytes)
            {
                return null;
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        return buffer.Length == 0 ? null : buffer.ToArray();
    }
}
