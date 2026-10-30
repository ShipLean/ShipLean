using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using static ShipLean.AcceptanceTests.Lanes.Sprints.Capacity.CapacityApi;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Capacity;

// Storefront picks New Zealand with a public holiday inside Sprint 24, seeded through SQL (no internet). The test saves,
// so the class has its own database.
[TestFixture]
public class CapacityHolidayTests : SeededAcceptanceTest
{
    [Test]
    public async Task Capacity_WhenProjectHasACountryWithAHoliday_ShouldLowerTheDefaultDays()
    {
        var sprint = SeedIds.Sprints.Sprint24;
        await SeedHolidayAsync(sprint);
        var (workingDays, defaultDays) = await CalendarAsync(Page, sprint);
        defaultDays.ShouldBe(workingDays - 1);
        var seeded = await EntriesAsync(Page, sprint);
        seeded.Length.ShouldBeGreaterThan(0);

        var capacity = new CapacityPage(Page);
        await capacity.OpenAsync("SHIP", sprint);
        await Expect(capacity.Calendar).ToHaveTextAsync($"{workingDays} working days · 1 public holiday (NZ)");
        await Expect(capacity.DefaultMarker(seeded[0].Member)).ToBeVisibleAsync();
        await capacity.Velocity.FillAsync("40");

        // Everyone on the default days: (W - 1) / W of an equal share each.
        var expected = Share(40, seeded.Length, defaultDays, workingDays);
        await Expect(capacity.Days(seeded[0].Member)).ToHaveValueAsync("");
        await Expect(capacity.Days(seeded[0].Member)).ToHaveAttributeAsync("placeholder", $"{defaultDays}");
        await Expect(capacity.Points(seeded[0].Member)).ToHaveTextAsync($"{expected:0.##}");
        await Expect(capacity.Total).ToHaveTextAsync($"{expected * seeded.Length:0.##}");

        (await capacity.SaveAsync(sprint)).Ok.ShouldBeTrue();
        var saved = await EntriesAsync(Page, sprint);
        saved.ShouldAllBe(e => e.Days == null && e.Points == expected);
    }

    private async Task SeedHolidayAsync(Guid sprintId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        const string sql = """
            INSERT INTO holiday_country (code, name) VALUES ('NZ', 'New Zealand') ON CONFLICT DO NOTHING;
            UPDATE project SET holiday_country = 'NZ' WHERE key = 'SHIP';
            INSERT INTO public_holiday (country_code, date, name)
            SELECT 'NZ', d::date, 'Test Day'
            FROM sprint s, generate_series(s.start_date, s.end_date, interval '1 day') d
            WHERE s.id = @sprint AND extract(isodow FROM d) = 3
            ORDER BY d LIMIT 1;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("sprint", sprintId);
        await command.ExecuteNonQueryAsync();
    }
}
