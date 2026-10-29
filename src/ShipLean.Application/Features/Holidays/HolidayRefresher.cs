using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts.Holidays;

namespace ShipLean.Application.Features.Holidays;

// One refresh of the stored holidays from the feed. Nothing here throws except cancellation: a failed fetch is logged and the
// stored data stays as it was. A country is refreshed whole (this year and next) or not at all, in one transaction.
public sealed class HolidayRefresher(
    NpgsqlDataSource dataSource,
    NagerClient nager,
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<HolidayRefresher> logger)
{
    // The country list, then every tracked country. A failed country list does not stop the holidays. Returns false when any fetch failed,
    // so the job can retry soon instead of waiting a day.
    public async Task<bool> RefreshAllAsync(CancellationToken cancellationToken)
    {
        if (!nager.Enabled)
        {
            return true;
        }

        var succeeded = await RefreshCountriesAsync(cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // A country picked in project settings is tracked by the request that picked it; this catches one whose tracking failed after the save.
        // It reads project in SQL only: the job has no C# dependency on the Projects feature.
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE holiday_country SET tracked = true WHERE code IN (SELECT holiday_country FROM project WHERE holiday_country IS NOT NULL) AND NOT tracked",
            cancellationToken: cancellationToken));
        var tracked = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT code::text FROM holiday_country WHERE tracked ORDER BY code", cancellationToken: cancellationToken))).ToList();
        foreach (var code in tracked)
        {
            succeeded &= await TryRefreshCountryAsync(code, cancellationToken) is not null;
        }

        return succeeded;
    }

    // Returns whether the list was refreshed (or already empty of news); false when the fetch failed or came back empty.
    public async Task<bool> RefreshCountriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var countries = (await nager.CountriesAsync(cancellationToken))
                .Where(c => c.CountryCode is { Length: 2 } && !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => new { Code = c.CountryCode!.ToUpperInvariant(), Name = c.Name!.Trim() })
                .DistinctBy(c => c.Code)
                .ToList();
            if (countries.Count == 0)
            {
                // An empty list is more likely a broken feed than a world with no countries: keep what is stored.
                logger.LogWarning("The holiday feed listed no countries; keeping the stored list");
                return false;
            }

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO holiday_country (code, name) VALUES (@Code, @Name)
                ON CONFLICT (code) DO UPDATE SET name = EXCLUDED.name WHERE holiday_country.name <> EXCLUDED.name
                """, countries, cancellationToken: cancellationToken));
            return true;
        }
        catch (Exception failed) when (failed is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(failed, "Refreshing the holiday country list failed; keeping the stored list");
            return false;
        }
    }

    // This year's and next year's national holidays for one country. Returns whether the stored holidays changed.
    public async Task<bool> RefreshCountryAsync(string countryCode, CancellationToken cancellationToken)
        => await TryRefreshCountryAsync(countryCode, cancellationToken) ?? false;

    // As RefreshCountryAsync, but null when the refresh failed, so a caller can tell a failure from "nothing changed".
    public async Task<bool?> TryRefreshCountryAsync(string countryCode, CancellationToken cancellationToken)
    {
        if (!nager.Enabled)
        {
            return false;
        }

        try
        {
            var thisYear = clock.GetUtcNow().UtcDateTime.Year;
            var fetched = new List<NagerHoliday>();
            foreach (var year in new[] { thisYear, thisYear + 1 })
            {
                fetched.AddRange(await nager.HolidaysAsync(year, countryCode, cancellationToken));
            }

            var changed = await StoreAsync(countryCode, new DateOnly(thisYear, 1, 1), new DateOnly(thisYear + 1, 12, 31), fetched, cancellationToken);
            if (changed)
            {
                await NotifyAsync(countryCode, cancellationToken);
            }

            return changed;
        }
        catch (Exception failed) when (failed is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(failed, "Refreshing the holidays of {Country} failed; keeping the stored ones", countryCode);
            return null;
        }
    }

    // Archives the stored holidays in the window that the feed no longer has, and adds or revives the rest.
    private async Task<bool> StoreAsync(string countryCode, DateOnly from, DateOnly to, List<NagerHoliday> fetched, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var existing = (await connection.QueryAsync<StoredHoliday>(new CommandDefinition(
            """
            SELECT date, name, archived_at IS NULL AS live FROM public_holiday
            WHERE country_code = @countryCode AND date >= @from AND date <= @to
            """, new { countryCode, from, to }, transaction, cancellationToken: cancellationToken))).ToList();
        if (fetched.Count == 0 && existing.Any(h => h.Live))
        {
            // A feed that suddenly has no holidays at all is more likely broken than right: keep the stored ones and tell no one.
            logger.LogWarning("The holiday feed returned no holidays for {Country} while some are stored; keeping the stored ones", countryCode);
            return false;
        }

        var wanted = fetched.Select(h => (h.Date, Name: h.Name!.Trim())).ToHashSet();
        var live = existing.Where(h => h.Live).Select(h => (h.Date, h.Name)).ToHashSet();
        var gone = live.Where(h => !wanted.Contains(h)).ToList();
        var arrived = wanted.Where(h => !live.Contains(h)).ToList();
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var (date, name) in gone)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE public_holiday SET archived_at = @now WHERE country_code = @countryCode AND date = @date AND name = @name",
                new { now, countryCode, date, name }, transaction, cancellationToken: cancellationToken));
        }

        foreach (var (date, name) in arrived)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO public_holiday (country_code, date, name) VALUES (@countryCode, @date, @name)
                ON CONFLICT (country_code, date, name) DO UPDATE SET archived_at = NULL
                """, new { countryCode, date, name }, transaction, cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE holiday_country SET refreshed_at = @now WHERE code = @countryCode",
            new { now, countryCode }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return gone.Count > 0 || arrived.Count > 0;
    }

    // A listener that fails must not undo or hide the refresh.
    private async Task NotifyAsync(string countryCode, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IHolidaysChanged>().ChangedAsync(countryCode, cancellationToken);
        }
        catch (Exception failed) when (failed is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(failed, "Telling listeners that the holidays of {Country} changed failed", countryCode);
        }
    }

    private sealed record StoredHoliday(DateOnly Date, string Name, bool Live);
}
