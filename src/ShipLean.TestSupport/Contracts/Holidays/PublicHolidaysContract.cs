using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Holidays;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Holidays;

// Behaviour every IPublicHolidays must have, whatever it stores data in. Each test arranges its own countries under codes
// no other test uses, so one store can serve the whole class.
[TestFixture]
public abstract class PublicHolidaysContract
{
    protected abstract IPublicHolidays CreateStore();

    protected abstract Task ArrangeCountryAsync(string code, string name);

    protected abstract Task ArrangeHolidayAsync(string countryCode, DateOnly date, string name);

    [Test]
    public async Task Countries_WhenArranged_ShouldListThemByName()
    {
        await ArrangeCountryAsync("ZB", "Zebraland");
        await ArrangeCountryAsync("ZA", "Aardvarkia");

        var listed = await CreateStore().CountriesAsync();

        listed.Where(c => c.Code is "ZA" or "ZB").ShouldBe([new HolidayCountryDto("ZA", "Aardvarkia"), new HolidayCountryDto("ZB", "Zebraland")]);
    }

    [Test]
    public async Task Holidays_WhenInRange_ShouldReturnThemOldestFirstAndInclusive()
    {
        await ArrangeCountryAsync("YA", "Yaland");
        await ArrangeCountryAsync("YB", "Ybland");
        await ArrangeHolidayAsync("YA", new DateOnly(2031, 12, 25), "Christmas");
        await ArrangeHolidayAsync("YA", new DateOnly(2031, 1, 1), "New Year");
        await ArrangeHolidayAsync("YA", new DateOnly(2031, 1, 1), "Another New Year");
        await ArrangeHolidayAsync("YA", new DateOnly(2032, 1, 1), "Next year");
        await ArrangeHolidayAsync("YB", new DateOnly(2031, 6, 6), "Elsewhere");

        var listed = await CreateStore().HolidaysAsync("YA", new DateOnly(2031, 1, 1), new DateOnly(2031, 12, 25));

        listed.ShouldBe([
            new PublicHolidayDto(new DateOnly(2031, 1, 1), "Another New Year"),
            new PublicHolidayDto(new DateOnly(2031, 1, 1), "New Year"),
            new PublicHolidayDto(new DateOnly(2031, 12, 25), "Christmas"),
        ]);
    }

    [Test]
    public async Task Holidays_WhenCodeIsLowerCase_ShouldMatchTheCountry()
    {
        await ArrangeCountryAsync("XA", "Xaland");
        await ArrangeHolidayAsync("XA", new DateOnly(2031, 3, 3), "Founders Day");

        var listed = await CreateStore().HolidaysAsync("xa", new DateOnly(2031, 1, 1), new DateOnly(2031, 12, 31));

        listed.Select(h => h.Name).ShouldBe(["Founders Day"]);
    }

    [Test]
    public async Task Holidays_WhenCountryHasNone_ShouldBeEmpty()
    {
        var listed = await CreateStore().HolidaysAsync("QQ", new DateOnly(2031, 1, 1), new DateOnly(2031, 12, 31));

        listed.ShouldBeEmpty();
    }

    [Test]
    public async Task Track_WhenCountryKnown_ShouldSucceedAndBeRepeatable()
    {
        await ArrangeCountryAsync("WA", "Waland");
        var store = CreateStore();

        await Should.NotThrowAsync(() => store.TrackAsync("WA"));
        await Should.NotThrowAsync(() => store.TrackAsync("wa"));
    }

    [Test]
    public async Task Track_WhenCountryUnknown_ShouldBeValidation()
    {
        var thrown = await Should.ThrowAsync<ApiException>(() => CreateStore().TrackAsync("VV"));

        thrown.Status.ShouldBe(400);
    }
}
