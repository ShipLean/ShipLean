using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// The only way into /mcp. A request is refused when its Origin is not the server's own (403), and when its bearer is missing,
// unknown, expired, revoked, for another audience or for a disabled user (401; 429 once its network has failed too many
// lookups in a minute, but only for a bad token: a valid one always gets in). The token is checked on every request (IAgentTokens), and never logged. On
// success the principal is the token's user, with the granted scopes as claims, and the request's agent is set.
//
// When SHIPLEAN_MCP_GATEWAY_EMAIL_HEADER is set and the request has no bearer, that header is the person: found or created
// by email (no name, so a new user's name is the part before @ and a later call does not rename them), with every scope and
// no agent. A blank, repeated or unusable header counts as no header. A bearer that does not validate is not rescued by the header.
public sealed class AgentBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAgentTokens tokens,
    AgentBaseUrl baseUrl,
    AgentFailureLimiter limiter,
    McpGatewaySettings gateway,
    IUserDirectory directory)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string RefusalItem = "shiplean.mcp.refusal";
    private const string BearerPrefix = "Bearer ";

    private enum Refusal
    {
        Foreign,
        TooManyFailures,
        BadToken,
    }

    private AgentAddresses? _addresses;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var addresses = await AddressesAsync();
        if (Request.Headers.TryGetValue(HeaderNames.Origin, out var origin) && !addresses.IsOwnOrigin(origin.ToString()))
        {
            Context.Items[RefusalItem] = Refusal.Foreign;
            return AuthenticateResult.Fail("Foreign origin.");
        }

        var header = Request.Headers.Authorization.ToString();
        if (header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase) && header[BearerPrefix.Length..].Trim() is { Length: > 0 } bearer)
        {
            return await AuthenticateBearerAsync(addresses, bearer);
        }

        return await AuthenticateGatewayAsync();
    }

    private async Task<AuthenticateResult> AuthenticateBearerAsync(AgentAddresses addresses, string bearer)
    {
        var agent = await tokens.ValidateAsync(bearer, addresses.Resource, Context.RequestAborted);
        if (agent is null)
        {
            Context.Items[RefusalItem] = limiter.TryRecordFailure(Context.Connection.RemoteIpAddress) ? Refusal.BadToken : Refusal.TooManyFailures;
            return AuthenticateResult.Fail("Invalid token.");
        }

        Context.Items[RequestCurrentAgent.ItemKey] = new AgentIdentity(agent.ClientName, agent.ClientHost, agent.Verified);
        return AuthenticateResult.Success(new AuthenticationTicket(AgentClaims.For(agent), Scheme.Name));
    }

    // No bearer. The header is the person only when the setting names one and the request carries a single usable email.
    private async Task<AuthenticateResult> AuthenticateGatewayAsync()
    {
        if (gateway.EmailHeader is not { } headerName || EmailFrom(headerName) is not { } email)
        {
            return AuthenticateResult.NoResult();
        }

        var user = await directory.FindOrCreateAsync(email, null, Context.RequestAborted);
        if (user.Status == UserStatus.Disabled)
        {
            Logger.LogInformation("MCP gateway refused user {UserId}: the account is disabled", user.Id);
            return AuthenticateResult.Fail("Disabled user.");
        }

        return AuthenticateResult.Success(new AuthenticationTicket(AgentClaims.ForGateway(user.Id), Scheme.Name));
    }

    // The header's one usable email, or null when it is absent, blank, repeated or not an email. The value is never logged.
    private string? EmailFrom(string headerName)
    {
        var values = Request.Headers[headerName];
        if (values.Count == 0)
        {
            return null;
        }
        if (values.Count != 1 || values[0]?.Trim() is not { Length: > 0 } raw)
        {
            Logger.LogWarning("MCP gateway ignored the {Header} header: it was blank or sent more than once.", headerName);
            return null;
        }
        if (IdentityInput.Email(raw) is not { } email)
        {
            Logger.LogWarning("MCP gateway ignored the {Header} header: it is not a usable email.", headerName);
            return null;
        }
        return email;
    }

    // A refused agent never sees a redirect: 403 for a foreign origin, 429 for a blocked address, otherwise 401 with the
    // resource metadata and the scopes a first connection asks for (read and write; plan comes by step-up).
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var refusal = Context.Items[RefusalItem] as Refusal?;
        switch (refusal)
        {
            case Refusal.Foreign:
                await WriteAsync(StatusCodes.Status403Forbidden, ApiException.Forbidden("This request came from another origin."));
                return;
            case Refusal.TooManyFailures:
                Response.Headers.RetryAfter = "60";
                await WriteAsync(StatusCodes.Status429TooManyRequests, ApiException.RateLimited("Too many failed attempts. Wait a minute and try again."));
                return;
        }

        var addresses = await AddressesAsync();
        var challenge = $"Bearer realm=\"shiplean\", resource_metadata=\"{addresses.ResourceMetadataUrl}\", scope=\"{McpScopes.Read} {McpScopes.Write}\"";
        if (refusal == Refusal.BadToken)
        {
            challenge += ", error=\"invalid_token\"";
        }
        Response.Headers.Append(HeaderNames.WWWAuthenticate, challenge);
        await WriteAsync(StatusCodes.Status401Unauthorized, ApiException.Unauthorized("Connect this agent to ShipLean again."));
    }

    private Task WriteAsync(int status, ApiException error)
    {
        Response.StatusCode = status;
        return Response.WriteAsJsonAsync(error.ToApiError(), JsonSerializerOptions.Web, Context.RequestAborted);
    }

    private async Task<AgentAddresses> AddressesAsync()
        => _addresses ??= await baseUrl.ReadAsync(Request, Context.RequestAborted);
}
