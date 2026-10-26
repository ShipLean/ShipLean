namespace ShipLean.Contracts.Profile;

// PUT /api/me/profile: the caller's display name and avatar colour (one of the design's identity colours, lowercase #rrggbb).
// Initials are derived from the name. The answer is the caller's MeDto.
public sealed record UpdateProfileRequest(string Name, string AvatarColour);
