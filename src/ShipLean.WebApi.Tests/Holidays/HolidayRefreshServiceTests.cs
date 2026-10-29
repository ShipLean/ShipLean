using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Holidays;
using ShipLean.Contracts.Holidays;
using ShipLean.TestSupport.Database;
using Shouldly;

namespace ShipLean.WebApi.Tests.Holidays;

// The real hosted service (ShipLeanHost removes it by default) over the stub feed: it refreshes on start, and a tracked country
// is refreshed through the queue without blocking the caller.
[TestFixture]
public class HolidayRefreshServiceTests : HolidayFeedFixture
{
    protected override void ConfigureMore(IServiceCollection services)
    {
        FeedCountries(("NZ", "New Zealand"), ("AU", "Australia"));
        FeedHolidays(ThisYear, "AU", ($"{ThisYear}-01-26", "Australia Day", true));
        FeedHolidays(ThisYear + 1, "AU", ($"{ThisYear + 1}-01-26", "Australia Day", true));
        FeedHolidays(ThisYear, "NZ", ($"{ThisYear}-02-06", "Waitangi Day", true));
        FeedHolidays(ThisYear + 1, "NZ");
        services.AddHostedService<HolidayRefreshService>();
    }

    protected override async Task<ClonedDatabase> CloneDatabaseAsync()
    {
        var database = await base.CloneDatabaseAsync();
        await using var connection = await database.OpenAsync();
        await connection.ExecuteAsync("INSERT INTO holiday_country (code, name, tracked) VALUES ('AU', 'Australia', true)");
        return database;
    }

    [Test, Order(1)]
    public async Task Start_WhenFeedAnswers_ShouldStoreTheCountriesAndTheTrackedOnesHolidays()
    {
        await Changes.NextAsync();

        (await Api.GetJsonAsync<List<HolidayCountryDto>>("api/holidays/countries")).Count.ShouldBe(2);
        (await StoredAsync("AU")).Count.ShouldBe(2);
        (await StoredAsync("NZ")).ShouldBeEmpty();
    }

    [Test, Order(2)]
    public async Task Track_WhenCountryKnown_ShouldQueueARefreshOfIt()
    {
        using var scope = Host.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IPublicHolidays>().TrackAsync("NZ");

        await Changes.NextAsync();
        (await StoredAsync("NZ")).Count.ShouldBe(1);
        Changes.Codes.ShouldBe(["AU", "NZ"]);
    }
}
