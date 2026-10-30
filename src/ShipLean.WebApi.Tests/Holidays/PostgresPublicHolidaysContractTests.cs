using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Holidays;
using ShipLean.Contracts.Holidays;
using ShipLean.TestSupport.Contracts.Holidays;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Holidays;

// PublicHolidaysContract on Postgres: one seeded clone for the class.
[TestFixture]
public class PostgresPublicHolidaysContractTests : PublicHolidaysContract
{
    private ClonedDatabase _database = null!;
    private NpgsqlDataSource _dataSource = null!;

    [OneTimeSetUp]
    public async Task CreateDatabase()
    {
        _database = await ShipLeanDatabase.CloneSeedAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_database.ConnectionString));
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override IPublicHolidays CreateStore() => new PublicHolidayStore(_dataSource, new HolidayRefreshQueue());

    protected override async Task ArrangeCountryAsync(string code, string name)
    {
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync("INSERT INTO holiday_country (code, name) VALUES (@code, @name)", new { code, name });
    }

    protected override async Task ArrangeHolidayAsync(string countryCode, DateOnly date, string name)
    {
        await using var connection = await _database.OpenAsync();
        await connection.ExecuteAsync("INSERT INTO public_holiday (country_code, date, name) VALUES (@countryCode, @date, @name)", new { countryCode, date, name });
    }
}
