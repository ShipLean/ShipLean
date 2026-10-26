using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Agents.Clients;

// An OAuth error body (RFC 7591 section 3.2.2). The /oauth/* endpoints speak OAuth, not ApiError.
public sealed record OAuthErrorDto(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string ErrorDescription);
