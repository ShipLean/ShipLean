namespace ShipLean.Contracts.Demos;

public sealed record ProjectDemoBrandDto(string? LogoUrl, string? LogoContentType, string Tokens, IReadOnlyList<DemoDefaultTokenDto> DefaultTokens);
