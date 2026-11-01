using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Retros.Sprint;

// GET api/sprints/{id}/retro, arranged with RetroSeed, RetroHistory and four ended sprints of a project of the test's own:
// S1 5-18 Jan, S2 19 Jan-1 Feb, S3 2-15 Feb, S4 16 Feb-1 Mar 2026. The sprint under test is S4 unless a test says otherwise.
public sealed class SprintRetroReaderTests : ApiFixture
{
    private static readonly Guid Joel = SeedIds.Users.Joel;

    [Test]
    public async Task Get_WhenStoryRetrosAndFeatureRetro_ShouldGroupByThemeLargestFirst()
    {
        await using var world = await WorldAsync();
        var story = await world.StoryAsync(world.S4);
        var feature = await world.ItemAsync("feature");
        var slow = await world.Seed.ThemeAsync("Slow CI");
        var flaky = await world.Seed.ThemeAsync("Flaky tests");
        var storyRetro = await world.RetroAsync(story, world.S4);
        var featureRetro = await world.RetroAsync(feature, world.S4);
        await world.Seed.PointAsync(storyRetro, new RetroSeedPoint(RetroCategory.WentBadly, "Flaky one.", ThemeId: flaky));
        await world.Seed.PointAsync(storyRetro, new RetroSeedPoint(RetroCategory.WentBadly, "Slow story point.", ThemeId: slow));
        await world.Seed.PointAsync(featureRetro, new RetroSeedPoint(RetroCategory.WentBadly, "Slow feature point.", ThemeId: slow));
        await world.Seed.PointAsync(storyRetro, new RetroSeedPoint(RetroCategory.WentWell, "Pairing."));

        var retro = await GetAsync(world.S4);

        retro.Categories.Select(column => column.Category).ShouldBe([RetroCategory.WentBadly, RetroCategory.Improve, RetroCategory.NotInPlan, RetroCategory.WentWell]);
        var badly = retro.Categories[0].Groups;
        badly.Select(group => group.Theme!.Name).ShouldBe(["Slow CI", "Flaky tests"]);
        badly[0].Points.Select(point => point.Text).ShouldBe(["Slow feature point.", "Slow story point."]);
        badly[0].Points[0].IsFeature.ShouldBeTrue();
        retro.Cards.Count.ShouldBe(2);
        retro.Sprint.Id.ShouldBe(world.S4);
        retro.Sprints.Select(sprint => sprint.Name).ShouldBe(["S1", "S2", "S3", "S4"]);
    }

    [Test]
    public async Task Get_WhenUnthemedPoints_ShouldGroupLast()
    {
        await using var world = await WorldAsync();
        var theme = await world.Seed.ThemeAsync("Handoffs");
        var retroId = await world.RetroAsync(await world.StoryAsync(world.S4), world.S4);
        await world.Seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.Improve, "Loose one."));
        await world.Seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.Improve, "Loose two."));
        await world.Seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.Improve, "Themed.", ThemeId: theme));

        var retro = await GetAsync(world.S4);

        var groups = retro.Categories.Single(column => column.Category == RetroCategory.Improve).Groups;
        groups.Select(group => group.Theme?.Name).ShouldBe(["Handoffs", null]);
        groups[1].Points.Count.ShouldBe(2);
    }

    [Test]
    public async Task Get_WhenSprintLevelPersonPoint_ShouldShowWithoutItem()
    {
        await using var world = await WorldAsync();
        var retroId = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.S4));
        await world.Seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.Improve, "Fewer meetings."));

        var retro = await GetAsync(world.S4);

        var card = retro.Cards.Single();
        card.WorkItem.ShouldBeNull();
        card.Author.Role.ShouldBe(RetroRole.Person);
        var point = retro.Categories.Single(column => column.Category == RetroCategory.Improve).Groups.Single().Points.Single();
        point.WorkItem.ShouldBeNull();
        point.CanArchive.ShouldBeTrue();
        point.Sprint.ShouldNotBeNull().Id.ShouldBe(world.S4);
    }

    [Test]
    public async Task Get_WhenFinishedStoryHasNoRetro_ShouldBeMissing()
    {
        await using var world = await WorldAsync();
        var withRetro = await world.StoryAsync(world.S4);
        var without = await world.StoryAsync(world.S4);
        await world.Seed.PointAsync(await world.RetroAsync(withRetro, world.S4), new RetroSeedPoint(RetroCategory.WentWell, "Smooth."));

        var retro = await GetAsync(world.S4);

        retro.Coverage.Count.ShouldBe(2);
        retro.Coverage.Single(row => row.Item.Id == withRetro).Status.ShouldBe(RetroCoverage.Done);
        retro.Coverage.Single(row => row.Item.Id == without).Status.ShouldBe(RetroCoverage.Missing);
        retro.Kpis.ShouldBe(new RetroKpisDto(1, 2, 0, 0));
    }

    [Test]
    public async Task Get_WhenFeatureHasOpenStory_ShouldBeWaiting()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        var openChild = await RetroHistory.CreateAsync(world.Connection, new WorkItemBuilder(world.Project.Id).WithType("story").WithParent(feature));
        await RetroHistory.MoveToSprintAsync(world.Connection, openChild, world.S4, RetroArrangement.Noon(2026, 2, 17));
        var doneChild = await RetroHistory.CreateAsync(world.Connection, new WorkItemBuilder(world.Project.Id).WithType("story").WithParent(feature));
        await RetroHistory.MoveToSprintAsync(world.Connection, doneChild, world.S4, RetroArrangement.Noon(2026, 2, 17));
        await RetroHistory.CompleteAsync(world.Connection, doneChild, RetroArrangement.Noon(2026, 2, 20));

        var retro = await GetAsync(world.S4);

        retro.Coverage.Single(row => row.Item.Id == feature).Status.ShouldBe(RetroCoverage.Waiting);
        retro.Coverage.Single(row => row.Item.Id == doneChild).Status.ShouldBe(RetroCoverage.Missing);
        retro.Kpis.Finished.ShouldBe(1);

        await RetroHistory.MoveToSprintAsync(world.Connection, openChild, world.S5, RetroArrangement.Noon(2026, 3, 2));

        var later = await GetAsync(world.S4);

        later.Coverage.Single(row => row.Item.Id == feature).Status.ShouldBe(RetroCoverage.Waiting);
        later.Coverage.Single(row => row.Item.Id == doneChild).Status.ShouldBe(RetroCoverage.Missing);
        later.Kpis.Finished.ShouldBe(1);
    }

    [Test]
    public async Task Get_WhenThemeIn3Of4Sprints_ShouldMarkRecurring()
    {
        await using var world = await WorldAsync();
        var recurring = await world.Seed.ThemeAsync("Review waits");
        var once = await world.Seed.ThemeAsync("One-off");
        foreach (var sprint in new[] { world.S2, world.S3, world.S4 })
        {
            var retroId = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: sprint));
            await world.Seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.WentBadly, "Waited on review.", ThemeId: recurring));
        }

        var onceRetro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.S4));
        await world.Seed.PointAsync(onceRetro, new RetroSeedPoint(RetroCategory.WentBadly, "Once.", ThemeId: once));

        var retro = await GetAsync(world.S4);

        var groups = retro.Categories[0].Groups;
        groups.Single(group => group.Theme!.Name == "Review waits").Recurring.ShouldBeTrue();
        groups.Single(group => group.Theme!.Name == "One-off").Recurring.ShouldBeFalse();
        groups.Single(group => group.Theme!.Name == "Review waits").Points.ShouldAllBe(point => point.Recurring);
        var strip = retro.Recurring.Single();
        strip.Theme.Name.ShouldBe("Review waits");
        strip.Counts.ShouldBe([0, 1, 1, 1]);
        strip.FixAt.ShouldBeNull();
        retro.Kpis.RecurringThemes.ShouldBe(1);
    }

    [Test]
    public async Task Get_WhenThemeMentionedInS1ToS3Only_ShouldStillBeRecurringInS4()
    {
        await using var world = await WorldAsync();
        var fading = await world.Seed.ThemeAsync("Fading");
        foreach (var sprint in new[] { world.S1, world.S2, world.S3 })
        {
            await world.MentionAsync(sprint, fading);
        }
        await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.S4));

        var retro = await GetAsync(world.S4);

        var strip = retro.Recurring.Single();
        strip.Theme.Name.ShouldBe("Fading");
        strip.Counts.ShouldBe([1, 1, 1, 0]);
        retro.Kpis.RecurringThemes.ShouldBe(1);
        retro.Categories.ShouldAllBe(column => column.Groups.Count == 0);
    }

    [Test]
    public async Task Get_WhenGoodToneThemeIn3Of4Sprints_ShouldNotBeRecurring()
    {
        await using var world = await WorldAsync();
        var good = await world.Seed.ThemeAsync("Pairing", RetroTone.Good);
        foreach (var sprint in new[] { world.S2, world.S3, world.S4 })
        {
            await world.MentionAsync(sprint, good);
        }

        var retro = await GetAsync(world.S4);

        retro.Recurring.ShouldBeEmpty();
        retro.Kpis.RecurringThemes.ShouldBe(0);
        retro.Categories[0].Groups.Single().Recurring.ShouldBeFalse();
    }

    [Test]
    public async Task Get_WhenFixHelpedBeforeTheWindow_ShouldNotBeRecurring()
    {
        await using var world = await WorldAsync();
        var a0 = await RetroArrangement.SprintAsync(world.Connection, world.Project.Id, "A0", new DateOnly(2025, 11, 10), new DateOnly(2025, 11, 23));
        await RetroArrangement.SprintAsync(world.Connection, world.Project.Id, "A1", new DateOnly(2025, 11, 24), new DateOnly(2025, 12, 7));
        await RetroArrangement.SprintAsync(world.Connection, world.Project.Id, "A2", new DateOnly(2025, 12, 8), new DateOnly(2025, 12, 21));
        await RetroArrangement.SprintAsync(world.Connection, world.Project.Id, "A3", new DateOnly(2025, 12, 22), new DateOnly(2026, 1, 4));
        var theme = await world.Seed.ThemeAsync("Review waits");
        await world.MentionAsync(a0, theme);
        await world.MentionAsync(a0, theme);
        foreach (var sprint in new[] { world.S2, world.S3, world.S4 })
        {
            await world.MentionAsync(sprint, theme);
        }
        var fix = await world.ItemAsync("action");
        await RetroHistory.MoveToSprintAsync(world.Connection, fix, a0, RetroArrangement.Noon(2025, 11, 11));
        await RetroHistory.CompleteAsync(world.Connection, fix, RetroArrangement.Noon(2025, 11, 20));
        await world.Seed.ActionAsync(new RetroSeedAction(fix, theme));

        var retro = await GetAsync(world.S4);

        retro.Recurring.ShouldBeEmpty();
        retro.Kpis.RecurringThemes.ShouldBe(0);
        retro.Sprints.Count.ShouldBe(6);
        retro.Categories[0].Groups.Single().Recurring.ShouldBeFalse();
    }

    [Test]
    public async Task Get_WhenActionClosedOnAGapDayAfterTheSprint_ShouldPlaceItInThatSprintLikeInsights()
    {
        await using var world = await WorldAsync();
        await world.Connection.ExecuteAsync("UPDATE sprint SET start_date = @start WHERE id = @id", new { start = new DateOnly(2026, 3, 5), id = world.S5 });
        var theme = await world.Seed.ThemeAsync("Review waits");
        foreach (var sprint in new[] { world.S2, world.S3, world.S4 })
        {
            await world.MentionAsync(sprint, theme);
        }
        var fix = await world.ItemAsync("action");
        await world.Seed.ActionAsync(new RetroSeedAction(fix, theme, RaisedSprintId: world.S4));
        await RetroHistory.CompleteAsync(world.Connection, fix, RetroArrangement.Noon(2026, 3, 3));

        var retro = await GetAsync(world.S4);
        using var scope = Host.Services.CreateScope();
        var insights = await scope.ServiceProvider.GetRequiredService<IRetroInsightsReader>().GetAsync(world.Project.Id, RetroRange.All, CancellationToken.None);

        retro.ActionItems.Single().Impact.ShouldBe(RetroVerdict.TooEarly);
        retro.Recurring.Single().FixAt.ShouldBe(3);
        insights.Impact.Single().ClosedAt.ShouldBe(3);
    }

    [Test]
    public async Task Get_WhenSprintIsArchivedAndHasNoEarlierLiveSprint_ShouldNotFail()
    {
        await using var world = await WorldAsync();
        await world.Connection.ExecuteAsync("UPDATE sprint SET archived_at = now() WHERE id = @id", new { id = world.S1 });

        using var response = await Api.GetAsync($"/api/sprints/{world.S1}/retro");

        ((int)response.StatusCode).ShouldBeLessThan(500);
    }

    [Test]
    public async Task Get_WhenArchivedPoint_ShouldLeaveItOut()
    {
        await using var world = await WorldAsync();
        var theme = await world.Seed.ThemeAsync("Gone");
        var retroId = await world.RetroAsync(await world.StoryAsync(world.S4), world.S4);
        await world.Seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.WentBadly, "Kept."));
        await world.Seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.WentBadly, "Archived.", ThemeId: theme, Archived: true));

        var retro = await GetAsync(world.S4);

        retro.Categories[0].Groups.SelectMany(group => group.Points).Select(point => point.Text).ShouldBe(["Kept."]);
        retro.Cards.Single().Points.Select(point => point.Text).ShouldBe(["Kept."]);
    }

    [Test]
    public async Task Get_WhenActionItemRaisedHere_ShouldListWithPlannedSprint()
    {
        await using var world = await WorldAsync();
        var theme = await world.Seed.ThemeAsync("Slow CI");
        var retroId = await world.RetroAsync(await world.StoryAsync(world.S4), world.S4);
        var pointId = await world.Seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.Improve, "Cache the build.", ThemeId: theme));
        var raisedHere = await world.ItemAsync("action");
        await RetroHistory.MoveToSprintAsync(world.Connection, raisedHere, world.S5, RetroArrangement.Noon(2026, 3, 2));
        await world.Seed.ActionAsync(new RetroSeedAction(raisedHere, theme, pointId, world.S4));
        var olderOpen = await world.ItemAsync("action");
        await world.Seed.ActionAsync(new RetroSeedAction(olderOpen, theme, null, world.S2));
        var unrelated = await world.ItemAsync("action");
        await world.Seed.ActionAsync(new RetroSeedAction(unrelated, null, null, world.S3));

        var retro = await GetAsync(world.S4);

        retro.ActionItems.Select(row => row.Action.WorkItemId).ShouldBe([raisedHere, olderOpen]);
        var row = retro.ActionItems[0];
        row.Action.Sprint.ShouldNotBeNull().Id.ShouldBe(world.S5);
        row.RaisedIn.ShouldNotBeNull().Id.ShouldBe(world.S4);
        row.Theme.ShouldNotBeNull().Name.ShouldBe("Slow CI");
        row.Impact.ShouldBeNull();
        retro.ActionItems[1].RaisedIn.ShouldNotBeNull().Id.ShouldBe(world.S2);
        retro.Kpis.ActionItemsRaised.ShouldBe(1);
        retro.Categories.Single(column => column.Category == RetroCategory.Improve).Groups.Single().Points.Single().Action.ShouldNotBeNull().WorkItemId.ShouldBe(raisedHere);
    }

    [Test]
    public async Task Get_WhenEmptySprint_ShouldReturnEmptyListsNotNull()
    {
        await using var world = await WorldAsync();

        var retro = await GetAsync(world.S4);

        retro.Cards.ShouldBeEmpty();
        retro.Coverage.ShouldBeEmpty();
        retro.ActionItems.ShouldBeEmpty();
        retro.Recurring.ShouldBeEmpty();
        retro.Categories.Count.ShouldBe(4);
        retro.Categories.ShouldAllBe(column => column.Groups.Count == 0);
        retro.Kpis.ShouldBe(new RetroKpisDto(0, 0, 0, 0));
        retro.Sprints.Count.ShouldBe(4);
        retro.Sprint.Status.ShouldBe(SprintStatus.Past);
    }

    [Test]
    public async Task Get_WhenNonMember_ShouldBeNotFound()
    {
        await using var world = await WorldAsync();

        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"/api/sprints/{world.S4}/retro");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    // The host serialises enums as camelCase strings; JsonSerializerOptions.Web alone doesn't read them back.
    private static readonly JsonSerializerOptions ResponseOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private async Task<SprintRetroDto> GetAsync(Guid sprintId)
    {
        using var response = await Api.GetAsync($"/api/sprints/{sprintId}/retro");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonSerializer.Deserialize<SprintRetroDto>(await response.Content.ReadAsStringAsync(), ResponseOptions)!;
    }

    private async Task<World> WorldAsync()
    {
        var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection, (Joel, ProjectRole.Member));
        return new World(
            connection,
            project,
            await RetroArrangement.SprintAsync(connection, project.Id, "S1", new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 18)),
            await RetroArrangement.SprintAsync(connection, project.Id, "S2", new DateOnly(2026, 1, 19), new DateOnly(2026, 2, 1)),
            await RetroArrangement.SprintAsync(connection, project.Id, "S3", new DateOnly(2026, 2, 2), new DateOnly(2026, 2, 15)),
            await RetroArrangement.SprintAsync(connection, project.Id, "S4", new DateOnly(2026, 2, 16), new DateOnly(2026, 3, 1)),
            await RetroArrangement.SprintAsync(connection, project.Id, "S5", new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 15)));
    }

    // The arranged project and sprints. It owns the connection until the fixture ends the scenario.
    private sealed record World(NpgsqlConnection Connection, ArrangedProject Project, Guid S1, Guid S2, Guid S3, Guid S4, Guid S5) : IAsyncDisposable
    {
        public RetroSeed Seed { get; } = new(Connection, Project.Id);

        public ValueTask DisposeAsync() => Connection.DisposeAsync();

        public async Task<Guid> ItemAsync(string type)
            => await RetroHistory.CreateAsync(Connection, new WorkItemBuilder(Project.Id).WithType(type));

        // A story finished in the sprint.
        public async Task<Guid> StoryAsync(Guid sprintId)
        {
            var id = await ItemAsync("story");
            await RetroHistory.MoveToSprintAsync(Connection, id, sprintId, RetroArrangement.Noon(2026, 2, 17));
            await RetroHistory.CompleteAsync(Connection, id, RetroArrangement.Noon(2026, 2, 20));
            return id;
        }

        // A went-badly point on the theme in a person retro of the sprint.
        public async Task MentionAsync(Guid sprintId, Guid themeId)
        {
            var retroId = await Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: sprintId));
            await Seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.WentBadly, "Mention.", ThemeId: themeId));
        }

        public Task<Guid> RetroAsync(Guid itemId, Guid sprintId)
            => Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.SubAgent, itemId, sprintId, $"agent-{Guid.NewGuid():N}"));
    }
}
