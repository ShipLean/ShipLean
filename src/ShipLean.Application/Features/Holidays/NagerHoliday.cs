namespace ShipLean.Application.Features.Holidays;

// One row of Nager.Date's PublicHolidays/{year}/{code}. Global is true for a national holiday, false for a regional one.
public sealed record NagerHoliday(DateOnly Date, string? Name, bool Global);
