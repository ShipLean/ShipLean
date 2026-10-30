namespace ShipLean.Contracts.Holidays;

// A country the holiday feed knows: its ISO 3166-1 alpha-2 code ("NZ") and English name.
public sealed record HolidayCountryDto(string Code, string Name);
