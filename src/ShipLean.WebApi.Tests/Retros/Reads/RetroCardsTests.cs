using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Retros.Reads;

// Cards for retros arranged with RetroSeed, read back through IRetroCards.
public sealed class RetroCardsTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    private IRetroCards Cards => Host.Services.GetRequiredService<IRetroCards>();

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    // Reads the retro page of a story as the user. The retro is written at ViewerZoneTestData.LateEveningUtc: 20:00 UTC, so the
    // next day in Bangkok. Time is never frozen: the expected label is built with DisplayDate and the zone.
    private async Task<RetroItemDto> ReadStoryRetroAsync(TestUser user, Guid userId)
    {
        await using var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection, (userId, ProjectRole.Member));
        var story = await new WorkItemBuilder(project.Id).WithType("story").InsertAsync(connection);
        var seed = new RetroSeed(connection, project.Id);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: story, AgentName: $"agent-{Guid.NewGuid():N}", CreatedAt: ViewerZoneTestData.LateEveningUtc));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Dated by the viewer."));
        var key = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story });
        using var response = await Api.ActingAs(user).GetAsync($"/api/projects/{project.Id}/retros/items/{key}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<RetroItemDto>(text, Wire)!;
    }

    // Mutation: pass TimeZoneInfo.Utc instead of the viewer's zone to the Relative call for CreatedAtLabel in RetroCards.ToCard.
    [Test]
    public async Task Cards_WhenViewerIsInBangkok_ShouldDateACardByTheBangkokDay()
    {
        await ViewerZoneArrange.SetAsync(Api, TestUser.Joel, ViewerZoneTestData.BangkokId);

        var item = await ReadStoryRetroAsync(TestUser.Joel, SeedIds.Users.Joel);

        var now = TimeProvider.System.GetUtcNow();
        var expected = DisplayDate.Relative(ViewerZoneTestData.LateEveningUtc, now, ViewerZoneTestData.Bangkok);
        expected.ShouldNotBe(DisplayDate.Relative(ViewerZoneTestData.LateEveningUtc, now, TimeZoneInfo.Utc));
        item.Cards.Single().CreatedAtLabel.ShouldBe(expected);
    }

    // The fallback: a user with no zone still gets UTC labels.
    [Test]
    public async Task Cards_WhenViewerHasNoZone_ShouldDateACardByTheUtcDay()
    {
        var item = await ReadStoryRetroAsync(TestUser.Priya, SeedIds.Users.Priya);

        item.Cards.Single().CreatedAtLabel.ShouldBe(DisplayDate.Relative(ViewerZoneTestData.LateEveningUtc, TimeProvider.System.GetUtcNow(), TimeZoneInfo.Utc));
    }

    [Test]
    public async Task Load_ShouldKeepTheGivenOrder()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var first = await AgentRetroAsync(seed, SeedIds.WorkItems.Ship90);
        var second = await AgentRetroAsync(seed, SeedIds.WorkItems.Ship91);
        var third = await AgentRetroAsync(seed, SeedIds.WorkItems.Ship92);

        var cards = await Cards.LoadAsync([third, first, second], CancellationToken.None);

        cards.Select(card => card.Id).ShouldBe([third, first, second]);
    }

    [Test]
    public async Task Load_ShouldBuildTheCreatedAtLabelOnTheServer()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await AgentRetroAsync(seed, SeedIds.WorkItems.Ship90);

        var card = (await Cards.LoadAsync([retro], CancellationToken.None)).Single();

        card.CreatedAtLabel.ShouldBe(DisplayDate.Relative(card.CreatedAt, TimeProvider.System.GetUtcNow(), TimeZoneInfo.Utc));
    }

    [Test]
    public async Task Load_ShouldLeaveOutArchivedPoints()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await AgentRetroAsync(seed, SeedIds.WorkItems.Ship90);
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Kept."));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Archived.", Archived: true));

        var card = (await Cards.LoadAsync([retro], CancellationToken.None)).Single();

        card.Points.Select(point => point.Text).ShouldBe(["Kept."]);
    }

    [Test]
    public async Task Load_ShouldGivePointsInPositionOrderWithThemeAndRootCause()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var theme = await seed.ThemeAsync($"Waits {Guid.NewGuid():N}");
        var retro = await AgentRetroAsync(seed, SeedIds.WorkItems.Ship90);
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Second.", Position: 1));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "First.", "Cause.", theme, Position: 0));

        var card = (await Cards.LoadAsync([retro], CancellationToken.None)).Single();

        card.Points.Select(point => point.Text).ShouldBe(["First.", "Second."]);
        card.Points[0].RootCause.ShouldBe("Cause.");
        card.Points[0].Theme.ShouldNotBeNull().Id.ShouldBe(theme);
        card.Points[0].Recurring.ShouldBeFalse();
        card.Points[0].CanArchive.ShouldBeFalse();
        card.Points[0].WorkItem.ShouldNotBeNull().Key.ShouldBe("SHIP-90");
        card.Author.Role.ShouldBe(RetroRole.SubAgent);
    }

    [Test]
    public async Task Load_ShouldGiveFailureLogInPositionOrder()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await AgentRetroAsync(seed, SeedIds.WorkItems.Ship90);
        await seed.FailureAsync(retro, RetroFailureKind.Blocker, "Second blocker.", 1);
        await seed.FailureAsync(retro, RetroFailureKind.Blocker, "First blocker.", 0);
        await seed.FailureAsync(retro, RetroFailureKind.Friction, "Some friction.", 2);
        await seed.FailureAsync(retro, RetroFailureKind.AssumptionDisproved, "Wrong guess.", 3);

        var log = (await Cards.LoadAsync([retro], CancellationToken.None)).Single().FailureLog;

        log.Blockers.ShouldBe(["First blocker.", "Second blocker."]);
        log.Friction.ShouldBe(["Some friction."]);
        log.AssumptionsDisproved.ShouldBe(["Wrong guess."]);
    }

    [Test]
    public async Task Load_WhenCompiledFromOtherRetros_ShouldGiveTheirItems()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var one = await AgentRetroAsync(seed, SeedIds.WorkItems.Ship90);
        var two = await AgentRetroAsync(seed, SeedIds.WorkItems.Ship91);
        var conductor = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Conductor, SeedIds.WorkItems.Ship104, AgentName: $"conductor-{Guid.NewGuid():N}"));
        await seed.SourceAsync(conductor, two);
        await seed.SourceAsync(conductor, one);

        var card = (await Cards.LoadAsync([conductor], CancellationToken.None)).Single();

        card.CompiledFrom.Select(item => item.Key).ShouldBe(["SHIP-90", "SHIP-91"]);
        card.WorkItem.ShouldNotBeNull().Type.Id.ShouldBe(WorkItemTypeId.Feature);
    }

    [Test]
    public async Task Load_WhenSprintLevelPersonRetro_ShouldHaveNoWorkItemAndArchivablePoints()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Priya, RetroRole.Person, SprintId: SeedIds.Sprints.Sprint24));
        await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Pair more."));

        var card = (await Cards.LoadAsync([retro], CancellationToken.None)).Single();

        card.WorkItem.ShouldBeNull();
        card.Sprint.ShouldNotBeNull().Id.ShouldBe(SeedIds.Sprints.Sprint24);
        card.Author.Role.ShouldBe(RetroRole.Person);
        card.Author.User.Id.ShouldBe(SeedIds.Users.Priya);
        card.Points.Single().CanArchive.ShouldBeTrue();
        card.Points.Single().Sprint.ShouldNotBeNull().Id.ShouldBe(SeedIds.Sprints.Sprint24);
    }

    [Test]
    public async Task Load_WhenPointHasAnAction_ShouldGiveItsRef()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await AgentRetroAsync(seed, SeedIds.WorkItems.Ship92);
        var point = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Do the thing."));
        await seed.ActionAsync(new RetroSeedAction(SeedIds.WorkItems.Ship89, PointId: point));

        var action = (await Cards.LoadAsync([retro], CancellationToken.None)).Single().Points.Single().Action;

        action.ShouldNotBeNull().Key.ShouldBe("SHIP-89");
    }

    [Test]
    public async Task Load_WhenActionItemIsRemoved_ShouldLeaveTheActionNull()
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var retro = await AgentRetroAsync(seed, SeedIds.WorkItems.Ship92);
        var point = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Do the thing."));
        var item = await new WorkItemBuilder(Project).WithType("action").WithTitle($"Fix {Guid.NewGuid():N}").InsertAsync(connection);
        await seed.ActionAsync(new RetroSeedAction(item, PointId: point));
        var before = (await Cards.LoadAsync([retro], CancellationToken.None)).Single().Points.Single().Action;
        await connection.ExecuteAsync(
            "UPDATE work_item SET state_id = (SELECT id FROM state WHERE project_id = @project AND type = 'action' AND category = 'removed' LIMIT 1) WHERE id = @action",
            new { project = Project, action = item });

        var after = (await Cards.LoadAsync([retro], CancellationToken.None)).Single().Points.Single().Action;

        before.ShouldNotBeNull();
        after.ShouldBeNull();
    }

    private static Task<Guid> AgentRetroAsync(RetroSeed seed, Guid workItemId)
        => seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: workItemId, AgentName: $"agent-{Guid.NewGuid():N}"));
}
