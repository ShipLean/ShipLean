namespace ShipLean.Contracts.Demos;

public sealed record DemoBrand(
    bool LogoIsDefault,
    string? LogoSvg,
    string? LogoUrl,
    string? LogoContentType,
    DateTimeOffset? LogoUrlExpiresAt,
    bool TokensAreDefault,
    string TokensCss,
    string? LogoFallbackReason);
