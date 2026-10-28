using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Features.LinearApi;

// The only way into /linear/graphql. `Authorization: <token>` (what @linear/sdk sends for an apiKey) or `Authorization: Bearer
// <token>` (an accessToken) is hashed and looked up among the live (not archived) connections; the principal is that
// connection. Anything else is 401 with Linear's error shape. The token is never logged.
public sealed class LinearTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAgentConnections connections)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString().Trim();
        var token = header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase) ? header[BearerPrefix.Length..].Trim() : header;
        if (token.Length == 0)
        {
            return AuthenticateResult.NoResult();
        }

        var connection = await connections.FindActiveByCallbackTokenHashAsync(IAgentConnections.HashCallbackToken(token), Context.RequestAborted);
        return connection is null
            ? AuthenticateResult.Fail("Unknown token.")
            : AuthenticateResult.Success(new AuthenticationTicket(LinearCaller.ToPrincipal(connection.Id, connection.Name), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Response.WriteAsJsonAsync(
            new { errors = new[] { new { message = "Authentication required: send the connection's callback token in the Authorization header.", extensions = new { type = "authentication error", code = "AUTHENTICATION_ERROR" } } } },
            JsonSerializerOptions.Web,
            Context.RequestAborted);
    }
}
