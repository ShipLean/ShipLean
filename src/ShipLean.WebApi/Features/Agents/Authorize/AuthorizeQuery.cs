using Microsoft.AspNetCore.Mvc;

namespace ShipLean.WebApi.Features.Agents.Authorize;

// The query string of GET /oauth/authorize (RFC 6749 4.1.1, PKCE RFC 7636, resource RFC 8707). Everything is optional here:
// the handler decides what a missing value means, so an absent parameter never becomes a model-state error page.
public sealed class AuthorizeQuery
{
    [FromQuery(Name = "response_type")]
    public string? ResponseType { get; init; }

    [FromQuery(Name = "client_id")]
    public string? ClientId { get; init; }

    [FromQuery(Name = "redirect_uri")]
    public string? RedirectUri { get; init; }

    [FromQuery(Name = "code_challenge")]
    public string? CodeChallenge { get; init; }

    [FromQuery(Name = "code_challenge_method")]
    public string? CodeChallengeMethod { get; init; }

    [FromQuery(Name = "resource")]
    public string? Resource { get; init; }

    [FromQuery(Name = "scope")]
    public string? Scope { get; init; }

    [FromQuery(Name = "state")]
    public string? State { get; init; }
}
