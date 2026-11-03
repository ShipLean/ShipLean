using ShipLean.AcceptanceTests.Harness;
using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Insights;

// A theme mentioned in four ended sprints is recurring, and the open action item that addresses it is listed. Once the
// item closes in an earlier sprint and a later sprint has ended, the Changes & impact view has its card. Seeded through
// SQL (RetroSeed, RetroHistory) on SHIP's Sprints 20-23, which have all ended.
[TestFixture]
public class RetroInsightsJourneyTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string ThemeName = "Flaky pipeline stage";
    private const string ActionTitle = "Quarantine the flaky pipeline stage";

    private static readonly Guid[] Mentioned = [SeedIds.Sprints.Sprint20, SeedIds.Sprints.Sprint21, SeedIds.Sprints.Sprint22, SeedIds.Sprints.Sprint23];

    [Test]
    public async Task Insights_WhenThemeRecurringAndFixed_ShouldShowImpact()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var action = await ArrangeAsync(connection);
        var insights = new RetroInsightsPage(Page);

        await insights.OpenAsync(Project);
        await Expect(insights.RecurringRowOf(ThemeName)).ToHaveAttributeAsync("data-status", "recurring");

        await insights.OpenAsync(Project, view: "actions");
        await Expect(insights.ActionRowOf(ActionTitle)).ToBeVisibleAsync();

        await insights.OpenAsync(Project, view: "impact");
        await Expect(insights.ImpactCards).ToHaveCountAsync(0);

        // The item closes during Sprint 22; Sprint 23 has ended since.
        await RetroHistory.CompleteAsync(connection, action, await NoonInAsync(connection, SeedIds.Sprints.Sprint22));
        await insights.OpenAsync(Project, view: "impact");
        await Expect(insights.ImpactCards).ToHaveCountAsync(1);
        await Expect(insights.ImpactCardOf(ActionTitle)).ToBeVisibleAsync();
        await Expect(insights.ImpactCardOf(ThemeName)).ToBeVisibleAsync();
    }

    [Test]
    public async Task Insights_WhenActionItemAddedFromTheHeader_ShouldListTheNewRowWithoutReloading()
    {
        const string title = "Stop the flaky stage from flaking";
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await ArrangeAsync(connection, "Header action theme", "Header action seed");
        var insights = new RetroInsightsPage(Page);
        var dialog = new RetroActionDialogPart(Page);

        await insights.OpenAsync(Project, view: "actions");
        await Expect(insights.ActionRowOf(title)).ToHaveCountAsync(0);
        await insights.ActionsAdd.ClickAsync();

        await Expect(dialog.Dialog).ToBeVisibleAsync();
        await Expect(dialog.Save).ToBeDisabledAsync();
        await dialog.Title.FillAsync(title);
        await Expect(dialog.Save).ToBeDisabledAsync();
        await dialog.Pick.First.GetByTestId(RetroSharedTestIds.RetroActionPickCheck).CheckAsync();
        await Expect(dialog.Save).ToBeEnabledAsync();
        await dialog.Save.ClickAsync();

        await Expect(dialog.Dialog).ToHaveCountAsync(0);
        await Expect(insights.ActionRowOf(title)).ToBeVisibleAsync();
        await Expect(insights.CoversOf(title)).ToHaveTextAsync("1");
    }

    private static async Task<Guid> ArrangeAsync(NpgsqlConnection connection, string themeName = ThemeName, string actionTitle = ActionTitle)
    {
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var theme = await seed.ThemeAsync(themeName);
        foreach (var sprint in Mentioned)
        {
            var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, SprintId: sprint));
            for (var i = 0; i < 2; i++)
            {
                await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"The stage flaked again ({sprint.ToString()[^2..]}-{i})", ThemeId: theme, CreatedAt: await NoonInAsync(connection, sprint)));
            }
        }

        var action = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(SeedIds.Projects.Ship).WithType("action").WithTitle(actionTitle));
        await seed.ActionAsync(new RetroSeedAction(action, theme, RaisedSprintId: SeedIds.Sprints.Sprint21));
        return action;
    }

    // Noon UTC on the fourth day of the sprint (sprint dates are computed from the run date).
    private static async Task<DateTimeOffset> NoonInAsync(NpgsqlConnection connection, Guid sprint)
    {
        var start = await connection.ExecuteScalarAsync<DateOnly>("SELECT start_date FROM sprint WHERE id = @sprint", new { sprint });
        var day = start.AddDays(3);
        return new DateTimeOffset(day.Year, day.Month, day.Day, 12, 0, 0, TimeSpan.Zero);
    }
}
