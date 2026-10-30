using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Holidays;

namespace ShipLean.Application.Fakes.Holidays;

// IPublicHolidays in memory, for tests that need holidays without a database or the feed. Arrange with AddCountry and AddHoliday.
public sealed class InMemoryPublicHolidays : IPublicHolidays
{
    private readonly Dictionary<string, HolidayCountryDto> _countries = [];
    private readonly HashSet<string> _tracked = [];
    private readonly List<(string Country, PublicHolidayDto Holiday)> _holidays = [];

    public IReadOnlyList<string> Tracked => [.. _tracked.Order(StringComparer.Ordinal)];

    public void AddCountry(string code, string name) => _countries[code.ToUpperInvariant()] = new HolidayCountryDto(code.ToUpperInvariant(), name);

    public void AddHoliday(string countryCode, DateOnly date, string name) => _holidays.Add((countryCode.ToUpperInvariant(), new PublicHolidayDto(date, name)));

    public Task<IReadOnlyList<HolidayCountryDto>> CountriesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<HolidayCountryDto>>([.. _countries.Values.OrderBy(c => c.Name, StringComparer.Ordinal).ThenBy(c => c.Code, StringComparer.Ordinal)]);

    public Task<IReadOnlyList<PublicHolidayDto>> HolidaysAsync(string countryCode, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var code = countryCode.Trim().ToUpperInvariant();
        return Task.FromResult<IReadOnlyList<PublicHolidayDto>>([.. _holidays
            .Where(h => h.Country == code && h.Holiday.Date >= from && h.Holiday.Date <= to)
            .Select(h => h.Holiday)
            .OrderBy(h => h.Date).ThenBy(h => h.Name, StringComparer.Ordinal)]);
    }

    public Task TrackAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        var code = countryCode.Trim().ToUpperInvariant();
        if (!_countries.ContainsKey(code))
        {
            throw ApiException.Validation("Pick a country from the list.");
        }

        _tracked.Add(code);
        return Task.CompletedTask;
    }
}
