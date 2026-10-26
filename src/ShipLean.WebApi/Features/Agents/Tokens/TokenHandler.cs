using Agoda.IoC.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Agents.Clients;

namespace ShipLean.WebApi.Features.Agents.Tokens;

// POST /oauth/token and /oauth/revoke. Public clients only: the client is named by client_id in the form, and a secret
// or Basic credentials are invalid_client. The resource must be this server's /mcp URL (invalid_target otherwise).
// Everything a store refuses (unknown, expired or spent code or refresh token, wrong verifier, redirect or client,
// revoked authorization, disabled or re-stamped user) is one invalid_grant, so a caller learns nothing about which.
[RegisterSingleton]
public sealed class TokenHandler(
    IOAuthCodes codes,
    IAgentTokenIssuer issuer,
    IAgentAddresses addresses,
    TimeProvider clock)
{
    // One text for every cause, so a caller learns nothing about which it was.
    private const string InvalidGrant = "The grant is invalid, expired or revoked.";

    private static readonly string[] ForbiddenCredentials = ["client_secret", "client_assertion", "client_assertion_type"];

    public async Task<IActionResult> TokenAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var form = await ReadFormAsync(request, cancellationToken);
        if (form is null)
        {
            return Error(StatusCodes.Status400BadRequest, "invalid_request", "Send application/x-www-form-urlencoded parameters, each at most once.");
        }
        if (UsesClientCredentials(request, form))
        {
            return Unauthorized("invalid_client", "This server has public clients only: send client_id in the body and no secret.");
        }
        if (Value(form, "client_id") is not { Length: > 0 } clientId)
        {
            return Error(StatusCodes.Status400BadRequest, "invalid_request", "client_id is required.");
        }

        var grantType = Value(form, "grant_type");
        if (grantType is not ("authorization_code" or "refresh_token"))
        {
            return Error(StatusCodes.Status400BadRequest, "unsupported_grant_type", "grant_type must be authorization_code or refresh_token.");
        }
        if (Value(form, "resource") is not { Length: > 0 } resource)
        {
            return Error(StatusCodes.Status400BadRequest, "invalid_request", "resource is required.");
        }
        if (!string.Equals(resource, (await addresses.ForCurrentRequestAsync(cancellationToken)).Resource, StringComparison.Ordinal))
        {
            return Error(StatusCodes.Status400BadRequest, "invalid_target", "resource must be this server's /mcp URL.");
        }

        return grantType == "refresh_token"
            ? await RefreshAsync(form, clientId, resource, cancellationToken)
            : await ExchangeCodeAsync(form, clientId, resource, cancellationToken);
    }

    public async Task<IActionResult> RevokeAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var form = await ReadFormAsync(request, cancellationToken);
        if (form is null || Value(form, "token") is not { Length: > 0 } token || Value(form, "client_id") is not { Length: > 0 } clientId)
        {
            return Error(StatusCodes.Status400BadRequest, "invalid_request", "token and client_id are required, as application/x-www-form-urlencoded parameters sent once.");
        }

        await issuer.RevokeAsync(token, clientId, cancellationToken);
        NoStore(request.HttpContext.Response);
        return new OkResult();
    }

    private async Task<IActionResult> ExchangeCodeAsync(IFormCollection form, string clientId, string resource, CancellationToken cancellationToken)
    {
        if (Value(form, "code") is not { Length: > 0 } code
            || Value(form, "code_verifier") is not { Length: > 0 } verifier
            || Value(form, "redirect_uri") is not { Length: > 0 } redirectUri)
        {
            return Error(StatusCodes.Status400BadRequest, "invalid_request", "code, code_verifier and redirect_uri are required.");
        }
        if (await codes.RedeemAsync(code, verifier, redirectUri, clientId, resource, cancellationToken) is not { } authorizationId)
        {
            return Error(StatusCodes.Status400BadRequest, "invalid_grant", InvalidGrant);
        }

        AgentTokenPair pair;
        try
        {
            pair = await issuer.IssueAsync(authorizationId, resource, cancellationToken);
        }
        catch (ApiException e) when (e.Code == ApiErrorCodes.NotFound)
        {
            return Error(StatusCodes.Status400BadRequest, "invalid_grant", InvalidGrant);
        }

        return Pair(pair);
    }

    private async Task<IActionResult> RefreshAsync(IFormCollection form, string clientId, string resource, CancellationToken cancellationToken)
    {
        if (Value(form, "refresh_token") is not { Length: > 0 } refreshToken)
        {
            return Error(StatusCodes.Status400BadRequest, "invalid_request", "refresh_token is required.");
        }
        return await issuer.RefreshAsync(refreshToken, clientId, resource, cancellationToken) is { } pair
            ? Pair(pair)
            : Error(StatusCodes.Status400BadRequest, "invalid_grant", InvalidGrant);
    }

    private IActionResult Pair(AgentTokenPair pair)
        => new TokenPairResult(new TokenResponseDto(
            pair.AccessToken, "Bearer", Math.Max(0, (int)(pair.AccessExpiresAt - clock.GetUtcNow()).TotalSeconds),
            pair.RefreshToken, string.Join(' ', pair.Scopes.Order(StringComparer.Ordinal))));

    // Null when the body is not a form, or repeats a parameter (RFC 6749 section 3.2).
    private static async Task<IFormCollection?> ReadFormAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType)
            || !mediaType.MediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        IFormCollection form;
        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (Exception e) when (e is InvalidDataException or BadHttpRequestException)
        {
            return null;
        }
        return form.Any(pair => pair.Value.Count > 1) ? null : form;
    }

    private static bool UsesClientCredentials(HttpRequest request, IFormCollection form)
        => request.Headers.Authorization.Count > 0 || ForbiddenCredentials.Any(form.ContainsKey);

    private static string? Value(IFormCollection form, string name) => form.TryGetValue(name, out StringValues values) ? values.ToString() : null;

    private static IActionResult Error(int status, string error, string description)
        => new TokenErrorResult(status, new OAuthErrorDto(error, description));

    private static IActionResult Unauthorized(string error, string description)
        => new TokenErrorResult(StatusCodes.Status401Unauthorized, new OAuthErrorDto(error, description));

    private static void NoStore(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers.Pragma = "no-cache";
    }

    private sealed class TokenPairResult(TokenResponseDto body) : ObjectResult(body)
    {
        public override void OnFormatting(ActionContext context)
        {
            NoStore(context.HttpContext.Response);
            base.OnFormatting(context);
        }
    }

    private sealed class TokenErrorResult : ObjectResult
    {
        public TokenErrorResult(int status, OAuthErrorDto body) : base(body) => StatusCode = status;

        public override void OnFormatting(ActionContext context)
        {
            NoStore(context.HttpContext.Response);
            if (StatusCode == StatusCodes.Status401Unauthorized)
            {
                context.HttpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"shiplean\"";
            }
            base.OnFormatting(context);
        }
    }
}
