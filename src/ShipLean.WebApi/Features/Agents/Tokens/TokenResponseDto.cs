using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Agents.Tokens;

// A successful token response (RFC 6749 section 5.1).
public sealed record TokenResponseDto(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string RefreshToken,
    [property: JsonPropertyName("scope")] string Scope);
