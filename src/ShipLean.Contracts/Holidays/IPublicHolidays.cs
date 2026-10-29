namespace ShipLean.Contracts.Holidays;

// Public holidays by country, as the background job last stored them. Sprints and project settings reach holidays only through
// this interface. Nothing fetched yet (or the feed switched off) is an empty list, never an error.
public interface IPublicHolidays
{
    // Every country the feed has listed, by name.
    Task<IReadOnlyList<HolidayCountryDto>> CountriesAsync(CancellationToken cancellationToken = default);

    // The country's national holidays from..to inclusive, by date then name; archived ones are left out. Codes are upper-case ISO.
    Task<IReadOnlyList<PublicHolidayDto>> HolidaysAsync(string countryCode, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    // Marks the country tracked (its holidays are kept up to date) and queues a refresh of it for the background job; it never
    // fetches on the caller's request. Validation for a code the feed has not listed.
    Task TrackAsync(string countryCode, CancellationToken cancellationToken = default);
}
