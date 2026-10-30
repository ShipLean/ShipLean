namespace ShipLean.Contracts.Holidays;

// One national public holiday on a calendar date (UTC date, no time).
public sealed record PublicHolidayDto(DateOnly Date, string Name);
