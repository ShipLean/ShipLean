using System.Text.RegularExpressions;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Sprints.List;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.AcceptanceTests.Pages.Sprints;
using static ShipLean.AcceptanceTests.Lanes.Sprints.Capacity.CapacityApi;

namespace ShipLean.AcceptanceTests.Journeys.Planning;

// A new project names its sprints by year and quarter. Its admin picks a country, plans the next sprint's capacity in days with
// one member away, moves the sprint's end and watches the default days follow, then switches to Number only. The holidays are
// seeded through SQL because the test host has no internet.
[TestFixture]
public class SprintNamingAndCapacityJourneyTests : SeededAcceptanceTest
{
    private const string ProjectName = "Journey Capacity";
    private const string ProjectKey = "JCP";
    private const string Teammate = "lee@shiplean.local";
    private const decimal Velocity = 40;
    private const decimal DaysAway = 5;

    [Test]
    public async Task J2_NewProject_NamesByQuarterAndPlansCapacityInDays()
    {
        await SeedCountryAsync();

        // 1. A new project defaults to Year, quarter and number: its first sprint and the next suggestion say so.
        var form = new NewProjectPage(Page);
        await form.OpenAsync();
        await form.Name.FillAsync(ProjectName);
        await form.Key.FillAsync(ProjectKey);
        await Expect(form.KeyFree).ToHaveAttributeAsync("data-ok", "true");
        var created = await form.CreateAsync();
        created.Ok.ShouldBeTrue(await created.TextAsync());
        var projectId = (await created.JsonAsync())!.Value.GetProperty("id").GetGuid();
        var settings = new SettingsDraftPage(Page);
        var quarterName = new Regex(@"^\d{4} Q[1-4] Sprint \d+$");

        await Page.GotoAsync(Links.ProjectSettings(ProjectKey, "sprints"));
        await Expect(settings.SprintNaming).ToHaveValueAsync("yearQuarterNumber");

        var sprints = new AllSprints(Page);
        await sprints.OpenAsync(ProjectKey);
        await Expect(sprints.Name).ToHaveValueAsync(quarterName);
        var suggested = await sprints.Name.InputValueAsync();
        var next = await sprints.CreateAsync(projectId.ToString());
        next.Ok.ShouldBeTrue(await next.TextAsync());
        var nextSprint = (await next.JsonAsync())!.Value.GetProperty("id").GetGuid();
        await Expect(sprints.RowOf(suggested)).ToBeVisibleAsync();

        // 2. The admin picks Australia and adds a teammate, then saves once.
        await Page.GotoAsync(Links.ProjectSettings(ProjectKey, "general"));
        await settings.HolidayCountry.SelectOptionAsync("AU");
        await settings.TabMembers.ClickAsync();
        await settings.AddMemberAsync(Teammate);
        await Expect(settings.MemberRows).ToHaveCountAsync(2);
        var savedSettings = await SaveSettingsAsync(projectId);
        savedSettings.Ok.ShouldBeTrue(await savedSettings.TextAsync());
        await Page.ReloadAsync();
        await settings.TabGeneral.ClickAsync();
        await Expect(settings.HolidayCountry).ToHaveValueAsync("AU");

        // 3. A public holiday lands on the first working day of the next sprint.
        await SeedHolidayAsync(nextSprint);
        var (workingDays, defaultDays) = await CalendarAsync(Page, nextSprint);
        defaultDays.ShouldBe(workingDays - 1);

        // 4. Capacity in days: velocity 40, two members, one away for a week.
        var members = await MemberNamesAsync(Page, ProjectKey);
        members.Length.ShouldBe(2);
        var away = members.Single(m => !m.StartsWith("Joel", StringComparison.Ordinal));
        var present = members.Single(m => m.StartsWith("Joel", StringComparison.Ordinal));
        var capacity = new CapacityPage(Page);
        await capacity.OpenAsync(ProjectKey, nextSprint);
        await Expect(capacity.Calendar).ToHaveTextAsync(new Regex($@"^{workingDays} working days · 1 public holiday \(AU\)"));
        await capacity.Velocity.FillAsync(Velocity.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var member in members)
        {
            await capacity.Add.SelectOptionAsync(new SelectOptionValue { Label = member });
        }

        await capacity.Days(away).FillAsync(DaysAway.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Expect(capacity.Days(present)).ToHaveValueAsync($"{defaultDays}");
        await Expect(capacity.DefaultMarker(present)).ToBeVisibleAsync();
        await Expect(capacity.Points(present)).ToHaveTextAsync($"{Share(Velocity, 2, defaultDays, workingDays):0.##}");
        await Expect(capacity.Points(away)).ToHaveTextAsync($"{Share(Velocity, 2, DaysAway, workingDays):0.##}");
        (await capacity.SaveAsync(nextSprint)).Ok.ShouldBeTrue();
        var plan = await EntriesAsync(Page, nextSprint);
        plan.Single(e => e.Member == present).Days.ShouldBeNull();
        plan.Single(e => e.Member == away).Days.ShouldBe(DaysAway);

        // 5. The next sprint's end moves out a week: the default days follow, the typed days stay.
        var dates = await SprintDatabase.ReadDatesAsync(ConnectionString, nextSprint);
        await sprints.OpenAsync(ProjectKey);
        await sprints.EditButtonOf(suggested).ClickAsync();
        await sprints.EditEnd.FillAsync(dates.End.AddDays(7).ToString("yyyy-MM-dd"));
        (await sprints.SaveEditAsync(nextSprint)).Ok.ShouldBeTrue();
        await Expect(sprints.EditRail).ToHaveCountAsync(0);

        var (longerDays, longerDefault) = await CalendarAsync(Page, nextSprint);
        longerDays.ShouldBe(workingDays + 5);
        longerDefault.ShouldBe(longerDays - 1);
        await capacity.OpenAsync(ProjectKey, nextSprint);
        await Expect(capacity.Calendar).ToHaveTextAsync(new Regex($@"^{longerDays} working days · 1 public holiday \(AU\)"));
        await Expect(capacity.Days(present)).ToHaveValueAsync($"{longerDefault}");
        await Expect(capacity.DefaultMarker(present)).ToBeVisibleAsync();
        await Expect(capacity.Days(away)).ToHaveValueAsync($"{DaysAway:0.#}");
        await Expect(capacity.Reset(away)).ToBeVisibleAsync();
        var follow = await EntriesAsync(Page, nextSprint);
        follow.Single(e => e.Member == present).Points.ShouldBe(Share(Velocity, 2, longerDefault, longerDays));
        follow.Single(e => e.Member == away).Points.ShouldBe(Share(Velocity, 2, DaysAway, longerDays));

        // 6. Number only: the next suggested name loses its year and quarter.
        await Page.GotoAsync(Links.ProjectSettings(ProjectKey, "sprints"));
        await settings.SprintNaming.SelectOptionAsync("number");
        var switched = await SaveSettingsAsync(projectId);
        switched.Ok.ShouldBeTrue(await switched.TextAsync());
        await sprints.OpenAsync(ProjectKey);
        await Expect(sprints.Name).ToHaveValueAsync(new Regex(@"^Sprint \d+$"));
        await Expect(sprints.RowOf(suggested)).ToBeVisibleAsync();
    }

    private Task<IResponse> SaveSettingsAsync(Guid projectId) =>
        Page.RunAndWaitForApiAsync($"/api/projects/{projectId}/settings", "PUT",
            () => new SettingsDraftPage(Page).SaveButton.ClickAsync());

    private async Task SeedCountryAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO holiday_country (code, name) VALUES ('AU', 'Australia') ON CONFLICT DO NOTHING", connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task SeedHolidayAsync(Guid sprintId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        const string sql = """
            INSERT INTO public_holiday (country_code, date, name)
            SELECT 'AU', d::date, 'Journey Day'
            FROM sprint s, generate_series(s.start_date, s.end_date, interval '1 day') d
            WHERE s.id = @sprint AND extract(isodow FROM d) BETWEEN 1 AND 5
            ORDER BY d LIMIT 1
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("sprint", sprintId);
        (await command.ExecuteNonQueryAsync()).ShouldBe(1);
    }
}
