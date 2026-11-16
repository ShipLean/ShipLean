namespace ShipLean.Contracts.Profile;

// GET /api/me/time-zones: every IANA zone the server knows, ordered by its offset now, then by id.
public sealed record TimeZoneChoicesDto(IReadOnlyList<TimeZoneChoiceDto> Choices);
