using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Holidays;

namespace ShipLean.Application.Features.Holidays;

// IPublicHolidays over the holiday tables the refresh job fills.
public sealed class PublicHolidayStore(NpgsqlDataSource dataSource, HolidayRefreshQueue queue) : IPublicHolidays
{
    public async Task<IReadOnlyList<HolidayCountryDto>> CountriesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<HolidayCountryDto>(new CommandDefinition(
            """SELECT code::text AS Code, name AS Name FROM holiday_country ORDER BY name COLLATE "C", code""", cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<PublicHolidayDto>> HolidaysAsync(string countryCode, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<PublicHolidayDto>(new CommandDefinition(
            """
            SELECT date AS Date, name AS Name FROM public_holiday
            WHERE country_code = @code AND archived_at IS NULL AND date >= @from AND date <= @to
            ORDER BY date, name
            """, new { code = countryCode.Trim().ToUpperInvariant(), from, to }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task TrackAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        var code = countryCode.Trim().ToUpperInvariant();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<TrackRow>(new CommandDefinition(
            "SELECT tracked, refreshed_at IS NOT NULL AS refreshed FROM holiday_country WHERE code = @code", new { code }, cancellationToken: cancellationToken));
        if (row is not { } country)
        {
            throw ApiException.Validation("Pick a country from the list.");
        }

        if (!country.Tracked)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE holiday_country SET tracked = true WHERE code = @code", new { code }, cancellationToken: cancellationToken));
        }

        if (!country.Tracked || !country.Refreshed)
        {
            queue.Enqueue(code);
        }
    }

    private sealed record TrackRow(bool Tracked, bool Refreshed);
}
