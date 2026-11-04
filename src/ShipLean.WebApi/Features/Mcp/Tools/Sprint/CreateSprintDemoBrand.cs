using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Sprint;

// The branding half of create_sprint_demo's result. The tokens and ShipLean's inline logo travel in the Markdown, not here.
public sealed record CreateSprintDemoBrand(
    [property: JsonPropertyName("logo_is_default")] bool LogoIsDefault,
    [property: JsonPropertyName("logo_url")] string? LogoUrl,
    [property: JsonPropertyName("logo_content_type")] string? LogoContentType,
    [property: JsonPropertyName("logo_url_expires_at")] DateTimeOffset? LogoUrlExpiresAt,
    [property: JsonPropertyName("tokens_are_default")] bool TokensAreDefault,
    [property: JsonPropertyName("logo_fallback_reason")] string? LogoFallbackReason);
