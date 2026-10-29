using ShipLean.Application.Fakes.Holidays;
using ShipLean.Contracts.Holidays;
using ShipLean.TestSupport.Contracts.Holidays;

namespace ShipLean.UnitTest.Contracts.Holidays;

[TestFixture]
public class InMemoryPublicHolidaysContractTests : PublicHolidaysContract
{
    private readonly InMemoryPublicHolidays _store = new();

    protected override IPublicHolidays CreateStore() => _store;

    protected override Task ArrangeCountryAsync(string code, string name)
    {
        _store.AddCountry(code, name);
        return Task.CompletedTask;
    }

    protected override Task ArrangeHolidayAsync(string countryCode, DateOnly date, string name)
    {
        _store.AddHoliday(countryCode, date, name);
        return Task.CompletedTask;
    }
}
