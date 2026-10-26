using System.Security.Claims;
using System.Text.RegularExpressions;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Features.Agents.Authorize;

// GET /oauth/authorize. Order matters: a signed-out browser goes to sign-in first, with no client lookup, and comes back to
// this exact URL. Then the client and its redirect URI, which must never redirect on failure. Then every protocol error:
// sent to the client's redirect URI (with error, error_description, state and iss, RFC 9207) only when that is loopback,
// else the error page. Then a password change, and last a pending request and the SPA consent page.
[RegisterSingleton]
public sealed partial class AuthorizeHandler(
    IOAuthClients clients,
    IOAuthRequests requests,
    IAgentAddresses addresses,
    UserSessions sessions)
{
    private static readonly string[] KnownScopes = [McpScopes.Read, McpScopes.Write, McpScopes.Plan];
    private static readonly string[] DefaultScopes = [McpScopes.Read, McpScopes.Write];

    // RFC 7636: the S256 challenge is 43 base64url characters.
    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex ChallengeShape();

    public async Task<IActionResult> HandleAsync(AuthorizeQuery query, ClaimsPrincipal principal, string pathAndQuery, CancellationToken cancellationToken)
    {
        var returnUrl = Uri.EscapeDataString(pathAndQuery);
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || await sessions.GetAsync(userId, cancellationToken) is not { Disabled: false } user)
        {
            return new RedirectResult($"/login?{CookieSessionEvents.ReturnUrlParameter}={returnUrl}");
        }

        if (string.IsNullOrWhiteSpace(query.ClientId))
        {
            throw ApiException.Validation("client_id is required.");
        }
        var client = await clients.FindAsync(query.ClientId, cancellationToken)
            ?? throw ApiException.Validation("This client is not known here.");
        if (string.IsNullOrWhiteSpace(query.RedirectUri) || !client.AllowsRedirect(query.RedirectUri))
        {
            throw ApiException.Validation("This redirect_uri is not registered for the client.");
        }

        var resolved = await addresses.ForCurrentRequestAsync(cancellationToken);
        // Only a loopback redirect is ever sent an error: a DCR client can register any https URI, and a malformed request must
        // not turn this endpoint into an open redirector (RFC 9700 4.11.2). Anything else gets the error page.
        IActionResult Refuse(string error, string description) => !OAuthClient.IsLoopback(query.RedirectUri)
            ? throw ApiException.Validation(description)
            : Redirect(query.RedirectUri, resolved.Issuer, query.State, ("error", error), ("error_description", description));

        if (query.ResponseType != "code")
        {
            return Refuse("unsupported_response_type", "response_type must be code.");
        }
        if (string.IsNullOrEmpty(query.CodeChallenge) || query.CodeChallengeMethod != "S256")
        {
            return Refuse("invalid_request", "PKCE is required: send a code_challenge with code_challenge_method=S256.");
        }
        if (!ChallengeShape().IsMatch(query.CodeChallenge))
        {
            return Refuse("invalid_request", "code_challenge is not a valid S256 challenge.");
        }
        if (query.Resource != resolved.Resource)
        {
            return Refuse("invalid_target", $"resource must be {resolved.Resource}.");
        }
        var scopes = (query.Scope ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
        if (scopes.Where(scope => !KnownScopes.Contains(scope)).ToArray() is { Length: > 0 } unknown)
        {
            return Refuse("invalid_scope", $"{unknown.Length} requested scope(s) are not known here. Known scopes: {string.Join(' ', KnownScopes)}.");
        }

        if (user.MustChangePassword)
        {
            return new RedirectResult($"/login?state=change&{CookieSessionEvents.ReturnUrlParameter}={returnUrl}");
        }

        var requestId = await requests.CreateAsync(
            new OAuthRequestCreate(client.ClientId, query.RedirectUri, query.State, query.CodeChallenge, scopes.Length == 0 ? DefaultScopes : scopes, resolved.Resource, userId),
            cancellationToken);
        return new RedirectResult($"{AgentUrls.ConsentPagePath}?request={requestId}");
    }

    private static RedirectResult Redirect(string redirectUri, string issuer, string? state, params (string Name, string Value)[] parameters)
    {
        var url = redirectUri;
        foreach (var (name, value) in parameters)
        {
            url = QueryHelpers.AddQueryString(url, name, value);
        }
        if (state is not null)
        {
            url = QueryHelpers.AddQueryString(url, "state", state);
        }
        return new RedirectResult(QueryHelpers.AddQueryString(url, "iss", issuer));
    }
}
