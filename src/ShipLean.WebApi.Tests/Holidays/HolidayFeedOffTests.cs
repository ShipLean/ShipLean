using ShipLean.Application.Features.Holidays;
using ShipLean.Contracts.Holidays;
using Shouldly;

namespace ShipLean.WebApi.Tests.Holidays;

// An instance with no internet sets SHIPLEAN_HOLIDAYS_API_URL empty: nothing is fetched and the list stays empty.
[TestFixture]
public class HolidayFeedOffTests : HolidayFeedFixture
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { [HolidaySettings.UrlKey] = string.Empty };

    [Test]
    public async Task RefreshAll_WhenFeedIsSwitchedOff_ShouldFetchNothing()
    {
        FeedCountries(("NZ", "New Zealand"));

        await Refresher.RefreshAllAsync(CancellationToken.None);

        Feed.Requests.ShouldBeEmpty();
        (await Api.GetJsonAsync<List<HolidayCountryDto>>("api/holidays/countries")).ShouldBeEmpty();
    }
}
