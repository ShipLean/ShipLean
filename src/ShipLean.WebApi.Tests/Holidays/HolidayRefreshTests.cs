using System.Net;
using ShipLean.Contracts.Holidays;
using Shouldly;

namespace ShipLean.WebApi.Tests.Holidays;

// HolidayRefresher against the stub feed, in order: one host and database for the class, each test building on the last.
[TestFixture]
public class HolidayRefreshTests : HolidayFeedFixture
{
    [Test, Order(1)]
    public async Task Countries_WhenNothingFetched_ShouldBeAnEmptyList()
    {
        var countries = await Api.GetJsonAsync<List<HolidayCountryDto>>("api/holidays/countries");

        countries.ShouldBeEmpty();
    }

    [Test, Order(2)]
    public async Task RefreshAll_WhenFeedListsCountries_ShouldStoreThemSortedByName()
    {
        FeedCountries(("NZ", "New Zealand"), ("AU", "Australia"), ("US", "United States"));

        await Refresher.RefreshAllAsync(CancellationToken.None);

        var countries = await Api.GetJsonAsync<List<HolidayCountryDto>>("api/holidays/countries");
        countries.ShouldBe([new HolidayCountryDto("AU", "Australia"), new HolidayCountryDto("NZ", "New Zealand"), new HolidayCountryDto("US", "United States")]);
        Feed.Requests.ShouldNotContain(r => r.Contains("PublicHolidays", StringComparison.Ordinal));
    }

    [Test, Order(3)]
    public async Task Countries_WhenFetched_ShouldHaveCodeAndNameOnly()
    {
        using var response = await Client.GetAsync("api/holidays/countries");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldStartWith("""[{"code":"AU","name":"Australia"},""");
    }

    [Test, Order(4)]
    public async Task RefreshAll_WhenCountryTracked_ShouldStoreGlobalHolidaysOfThisAndNextYear()
    {
        await TrackAsync("NZ");
        FeedHolidays(ThisYear, "NZ", ($"{ThisYear}-01-01", "New Year's Day", true), ($"{ThisYear}-03-05", "Taranaki Anniversary", false));
        FeedHolidays(ThisYear + 1, "NZ", ($"{ThisYear + 1}-01-01", "New Year's Day", true));

        await Refresher.RefreshAllAsync(CancellationToken.None);

        (await StoredAsync("NZ")).ShouldBe([($"{ThisYear}-01-01 New Year's Day", false), ($"{ThisYear + 1}-01-01 New Year's Day", false)]);
        (await StoredAsync("AU")).ShouldBeEmpty();
        Changes.Codes.ShouldBe(["NZ"]);
    }

    [Test, Order(5)]
    public async Task RefreshAll_WhenRunAgainWithSameData_ShouldNotDuplicateOrReportAChange()
    {
        await Refresher.RefreshAllAsync(CancellationToken.None);

        (await StoredAsync("NZ")).Count.ShouldBe(2);
        Changes.Codes.ShouldBe(["NZ"]);
    }

    [Test, Order(6)]
    public async Task RefreshAll_WhenFeedFails_ShouldKeepTheOldDataAndReportNothing()
    {
        Feed.Respond($"/api/v3/PublicHolidays/{ThisYear + 1}/NZ", "boom", "text/plain", status: 500);
        FeedHolidays(ThisYear, "NZ", ($"{ThisYear}-12-25", "Christmas Day", true));
        Feed.Respond("/api/v3/AvailableCountries", "boom", "text/plain", status: 500);

        await Refresher.RefreshAllAsync(CancellationToken.None);

        (await StoredAsync("NZ")).ShouldBe([($"{ThisYear}-01-01 New Year's Day", false), ($"{ThisYear + 1}-01-01 New Year's Day", false)]);
        (await Api.GetJsonAsync<List<HolidayCountryDto>>("api/holidays/countries")).Count.ShouldBe(3);
        Changes.Codes.ShouldBe(["NZ"]);
    }

    [Test, Order(7)]
    public async Task RefreshCountry_WhenHolidayLeavesTheFeed_ShouldArchiveItAndReportAChange()
    {
        FeedHolidays(ThisYear, "NZ", ($"{ThisYear}-12-25", "Christmas Day", true));
        FeedHolidays(ThisYear + 1, "NZ", ($"{ThisYear + 1}-01-01", "New Year's Day", true));

        var changed = await Refresher.RefreshCountryAsync("NZ", CancellationToken.None);

        changed.ShouldBeTrue();
        (await StoredAsync("NZ")).ShouldBe([($"{ThisYear}-01-01 New Year's Day", true), ($"{ThisYear}-12-25 Christmas Day", false), ($"{ThisYear + 1}-01-01 New Year's Day", false)]);
        Changes.Codes.ShouldBe(["NZ", "NZ"]);
    }

    [Test, Order(8)]
    public async Task RefreshCountry_WhenHolidayReturns_ShouldReviveTheArchivedRow()
    {
        FeedHolidays(ThisYear, "NZ", ($"{ThisYear}-01-01", "New Year's Day", true), ($"{ThisYear}-12-25", "Christmas Day", true));

        var changed = await Refresher.RefreshCountryAsync("NZ", CancellationToken.None);

        changed.ShouldBeTrue();
        (await StoredAsync("NZ")).ShouldBe([($"{ThisYear}-01-01 New Year's Day", false), ($"{ThisYear}-12-25 Christmas Day", false), ($"{ThisYear + 1}-01-01 New Year's Day", false)]);
    }
}
