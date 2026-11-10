using Dapper;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;
using ShipLean.Contracts.Views;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Screens;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// What the Stories backlog adds to its rows: what waits on the caller, the open blocked-by keys and a story's task counts. Arranged
// stories are the signed-in user's (Joel's), who owns nothing with an open ask in the seed.
[TestFixture]
public class StoriesBacklogNeedsYouApiTests : ApiFixture
{
    private static readonly string Url = $"api/projects/{SeedIds.Projects.Ship}/backlog";

    [Test]
    public async Task Stories_WhenStoryHasOpenAsks_ShouldCarryNeedsYou()
    {
        await using var connection = await OpenDatabaseAsync();
        var waiting = await NeedsYouArrangement.ItemAsync(connection, "story", SeedIds.Users.Joel);
        var quiet = await NeedsYouArrangement.ItemAsync(connection, "story", SeedIds.Users.Joel);
        await NeedsYouArrangement.AsksAsync(connection, waiting, questions: 1, decisions: 2);

        var list = await Api.GetBacklogAsync($"{Url}?mode=list");
        var tree = await Api.GetBacklogAsync($"{Url}?mode=tree");

        list.Rows.Single(r => r.Id == waiting).NeedsYou.ShouldBe(new NeedsYouDto(1, 2, false));
        list.Rows.Single(r => r.Id == quiet).NeedsYou.ShouldBeNull();
        tree.Rows.Single(r => r.Id == waiting).NeedsYou.ShouldBe(new NeedsYouDto(1, 2, false));
        list.Rows.ShouldAllBe(r => r.Health == null);
    }

    [Test]
    public async Task Stories_WhenPlanningMode_ShouldCarryNeedsYouInSections()
    {
        await using var connection = await OpenDatabaseAsync();
        var waiting = await NeedsYouArrangement.ItemAsync(connection, "story", SeedIds.Users.Joel);
        await NeedsYouArrangement.AsksAsync(connection, waiting, questions: 3, decisions: 0);

        var view = await Api.GetBacklogAsync($"{Url}?mode=planning");

        view.Sections.SelectMany(s => s.Rows).Single(r => r.Id == waiting).NeedsYou.ShouldBe(new NeedsYouDto(3, 0, false));
        view.Sections.SelectMany(s => s.Rows).Count(r => r.NeedsYou is not null).ShouldBe(1);
    }

    [Test]
    public async Task Stories_WhenStoryHasTasks_ShouldCarryTaskRollups()
    {
        await using var connection = await OpenDatabaseAsync();
        var story = await NeedsYouArrangement.ItemAsync(connection, "story", SeedIds.Users.Joel);
        var bare = await NeedsYouArrangement.ItemAsync(connection, "story", SeedIds.Users.Joel);
        await NeedsYouArrangement.ItemAsync(connection, "task", null, story);
        await NeedsYouArrangement.ItemAsync(connection, "task", null, story);
        await NeedsYouArrangement.ItemAsync(connection, "task", null, story, state: "Closed");
        await NeedsYouArrangement.ItemAsync(connection, "task", null, story, state: "Removed");

        var list = await Api.GetBacklogAsync($"{Url}?mode=list");
        var planning = await Api.GetBacklogAsync($"{Url}?mode=planning");

        list.Rows.Single(r => r.Id == story).Rollups.ShouldBe([new RollupCountDto(WorkItemTypeId.Task, 2, 1, 0, 0)]);
        list.Rows.Single(r => r.Id == bare).Rollups.ShouldBeEmpty();
        planning.Sections.SelectMany(s => s.Rows).Single(r => r.Id == story).Rollups.ShouldBe([new RollupCountDto(WorkItemTypeId.Task, 2, 1, 0, 0)]);
    }

    [Test]
    public async Task Backlog_WhenRowIsBlockedByAnOpenItem_ShouldListItsKey()
    {
        await using var connection = await OpenDatabaseAsync();
        var story = await NeedsYouArrangement.ItemAsync(connection, "story", SeedIds.Users.Joel);
        var free = await NeedsYouArrangement.ItemAsync(connection, "story", SeedIds.Users.Joel);
        var openBlocker = await NeedsYouArrangement.ItemAsync(connection, "story", SeedIds.Users.Joel);
        var closedBlocker = await NeedsYouArrangement.ItemAsync(connection, "story", SeedIds.Users.Joel, state: "Closed");
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, target_id, link_type) VALUES (gen_random_uuid(), @story, @openBlocker, 'blockedBy'), (gen_random_uuid(), @story, @closedBlocker, 'blockedBy')",
            new { story, openBlocker, closedBlocker });
        var openKey = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @openBlocker", new { openBlocker });

        var view = await Api.GetBacklogAsync($"{Url}?mode=list");

        view.Rows.Single(r => r.Id == story).BlockedBy.ShouldBe([openKey!]);
        view.Rows.Single(r => r.Id == free).BlockedBy.ShouldBeEmpty();
    }
}
