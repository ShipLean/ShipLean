using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Holidays;
using ShipLean.Contracts.Holidays;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Http;

namespace ShipLean.WebApi.Tests.Holidays;

// The app over a stub Nager.Date server on loopback: no real internet. The refresh job is not running (ShipLeanHost removes it);
// tests drive HolidayRefresher themselves, and a job test adds the hosted service back.
public abstract class HolidayFeedFixture : ApiFixture
{
    protected static readonly int ThisYear = TimeProvider.System.GetUtcNow().Year;

    protected StubHttpServer Feed { get; private set; } = null!;

    protected RecordingHolidaysChanged Changes { get; } = new();

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        [HolidaySettings.UrlKey] = Feed.UrlOf("/api/v3").ToString(),
    };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.Replace(ServiceDescriptor.Singleton<IHolidaysChanged>(Changes));
        ConfigureMore(services);
    };

    protected virtual void ConfigureMore(IServiceCollection services)
    {
    }

    protected override async Task BeforeHostStartAsync() => Feed = await StubHttpServer.StartAsync();

    protected override async Task AfterHostStopAsync() => await Feed.DisposeAsync();

    protected HolidayRefresher Refresher => Host.Services.GetRequiredService<HolidayRefresher>();

    protected void FeedCountries(params (string Code, string Name)[] countries)
        => Feed.Respond("/api/v3/AvailableCountries", JsonSerializer.Serialize(countries.Select(c => new { countryCode = c.Code, name = c.Name })), "application/json");

    protected void FeedHolidays(int year, string code, params (string Date, string Name, bool Global)[] holidays)
        => Feed.Respond($"/api/v3/PublicHolidays/{year}/{code}",
            JsonSerializer.Serialize(holidays.Select(h => new { date = h.Date, localName = h.Name, name = h.Name, countryCode = code, global = h.Global })), "application/json");

    protected async Task TrackAsync(string code)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE holiday_country SET tracked = true WHERE code = @code", new { code });
    }

    protected async Task<List<(string Name, bool Archived)>> StoredAsync(string code)
    {
        await using var connection = await OpenDatabaseAsync();
        var rows = await connection.QueryAsync<(string Name, bool Archived)>(
            "SELECT date::text || ' ' || name AS Name, archived_at IS NOT NULL AS Archived FROM public_holiday WHERE country_code = @code ORDER BY date, name", new { code });
        return rows.ToList();
    }
}
