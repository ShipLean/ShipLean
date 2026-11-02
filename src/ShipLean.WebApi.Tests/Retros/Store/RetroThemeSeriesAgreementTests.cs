using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Retros.Store;

// The sprint view, Insights and Search all apply the recurring rule through RetroThemeSeries, so one arranged history gives one answer.
// Four weekly sprints that all ended in March 2026 (Monday 2 March is S1).
public sealed class RetroThemeSeriesAgreementTests : ApiFixture
{
    private static readonly Guid Joel = SeedIds.Users.Joel;
    private static readonly DateOnly FirstMonday = new(2026, 3, 2);

    [Test]
    public async Task Recurring_WhenOneHistory_ShouldBeTheSameInSprintInsightsAndSearch()
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection, (Joel, ProjectRole.Member));
        var sprints = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var start = FirstMonday.AddDays(7 * i);
            sprints.Add(await RetroArrangement.SprintAsync(connection, project.Id, $"S{i + 1}", start, start.AddDays(6)));
        }

        var seed = new RetroSeed(connection, project.Id);
        var stuck = await seed.ThemeAsync("Stuck reviews");
        var fixedTheme = await seed.ThemeAsync("Fixed by an action");
        int[] fixedCounts = [4, 1, 1, 1];
        for (var sprint = 0; sprint < 4; sprint++)
        {
            var retro = await seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: sprints[sprint]));
            var at = Noon(sprint);
            await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"Stuck {sprint}", ThemeId: stuck, CreatedAt: at));
            for (var i = 0; i < fixedCounts[sprint]; i++)
            {
                await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, $"Fixed {sprint}-{i}", ThemeId: fixedTheme, CreatedAt: at.AddMinutes(i + 1)));
            }
        }

        // Closed during S1 but never sat in a sprint: its closing sprint is the one whose dates contain the completion date.
        var action = await RetroHistory.CreateAsync(connection, new WorkItemBuilder(project.Id).WithType("action").WithTitle("Fix reviews"));
        await seed.ActionAsync(new RetroSeedAction(action, fixedTheme, RaisedSprintId: sprints[3]));
        await RetroHistory.CompleteAsync(connection, action, Noon(0).AddHours(1));

        using var scope = Host.Services.CreateScope();
        var sprintView = await scope.ServiceProvider.GetRequiredService<ISprintRetroReader>().GetAsync(sprints[3], CancellationToken.None);
        var insights = await scope.ServiceProvider.GetRequiredService<IRetroInsightsReader>().GetAsync(project.Id, RetroRange.All, CancellationToken.None);
        var search = await scope.ServiceProvider.GetRequiredService<IRetroSearchReader>().SearchAsync(project.Id, new RetroSearchQuery(null, null, null, null, null, null, null, null, null, null, null), CancellationToken.None);

        sprintView.Recurring.Select(strip => strip.Theme.Id).ShouldBe([stuck]);
        insights.Themes.Where(row => row.Status == RetroThemeStatus.Recurring).Select(row => row.Theme.Id).ShouldBe([stuck]);
        search.Rows.Where(row => row.Recurring).Select(row => row.Theme!.Id).Distinct().ShouldBe([stuck]);
        insights.Themes.Single(row => row.Theme.Id == fixedTheme).LastVerdict.ShouldBe(RetroVerdict.Helped);
        sprintView.ActionItems.Single(row => row.Action.WorkItemId == action).Impact.ShouldBe(RetroVerdict.Helped);
    }

    private static DateTimeOffset Noon(int sprint)
    {
        var day = FirstMonday.AddDays((7 * sprint) + 2);
        return RetroArrangement.Noon(day.Year, day.Month, day.Day);
    }
}
