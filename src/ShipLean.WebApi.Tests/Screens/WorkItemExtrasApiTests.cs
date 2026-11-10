using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Screens;
using ShipLean.WebApi.Tests.Taskboard;

namespace ShipLean.WebApi.Tests.Screens;

// The work item read carries the roll-ups and what waits on the caller. Arranged items are the signed-in user's (Joel's), who owns
// nothing with an open ask in the seed.
[TestFixture]
public class WorkItemExtrasApiTests : ApiFixture
{
    private static readonly string ByKey = $"api/projects/{SeedIds.Projects.Ship}/work-items/by-key";

    private static readonly Guid Owner = SeedIds.Users.Joel;

    [Test]
    public async Task GetByKey_WhenFeature_ShouldCarryRollupsAndNeedsYou()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await NeedsYouArrangement.ItemAsync(connection, "feature", Owner);
        var story = await NeedsYouArrangement.ItemAsync(connection, "story", Owner, feature);
        await NeedsYouArrangement.ItemAsync(connection, "story", Owner, feature, state: "Closed");
        await NeedsYouArrangement.ItemAsync(connection, "task", null, story);
        await NeedsYouArrangement.AsksAsync(connection, story, questions: 2, decisions: 0);
        await NeedsYouArrangement.AsksAsync(connection, feature, questions: 0, decisions: 1);
        var key = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });

        var byKey = await Api.GetAsync<WorkItemDto>($"{ByKey}/{key}");
        var byId = await Api.GetAsync<WorkItemDto>($"api/work-items/{feature}");

        foreach (var item in new[] { byKey, byId })
        {
            item.Rollups.ShouldBe([new RollupCountDto(WorkItemTypeId.Story, 1, 1, 0, 0)]);
            item.NeedsYou.ShouldBe(new NeedsYouDto(2, 1, true));
        }
    }

    [Test]
    public async Task GetByKey_WhenStory_ShouldCarryTaskRollups()
    {
        await using var connection = await OpenDatabaseAsync();
        var story = await NeedsYouArrangement.ItemAsync(connection, "story", Owner);
        var bare = await NeedsYouArrangement.ItemAsync(connection, "story", Owner);
        var task = await NeedsYouArrangement.ItemAsync(connection, "task", Owner, story);
        await NeedsYouArrangement.ItemAsync(connection, "task", null, story, state: "Closed");
        var keys = (await connection.QueryAsync<(Guid Id, string Key)>("SELECT id, key FROM work_item WHERE id = ANY(@ids)", new { ids = new[] { story, bare, task } })).ToDictionary(r => r.Id, r => r.Key);

        var item = await Api.GetAsync<WorkItemDto>($"{ByKey}/{keys[story]}");
        var withoutTasks = await Api.GetAsync<WorkItemDto>($"{ByKey}/{keys[bare]}");
        var taskItem = await Api.GetAsync<WorkItemDto>($"{ByKey}/{keys[task]}");

        item.Rollups.ShouldBe([new RollupCountDto(WorkItemTypeId.Task, 1, 1, 0, 0)]);
        item.NeedsYou.ShouldBeNull();
        withoutTasks.Rollups.ShouldBeEmpty();
        taskItem.Rollups.ShouldBeEmpty();
        taskItem.NeedsYou.ShouldBeNull();
    }

    [Test]
    public async Task ApplyAsync_WhenNoCurrentUser_ShouldLeaveNeedsYouNull()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await NeedsYouArrangement.ItemAsync(connection, "feature", Owner);
        await NeedsYouArrangement.ItemAsync(connection, "story", Owner, feature);
        await NeedsYouArrangement.AsksAsync(connection, feature, questions: 1, decisions: 0);
        var read = await Api.GetAsync<WorkItemDto>($"api/work-items/{feature}");
        using var scope = Host.Services.CreateScope();
        var counts = new FakeNeedsYouCounts();
        var extras = new WorkItemExtras(scope.ServiceProvider.GetRequiredService<IWorkTreeReads>(), counts, new NoUser());

        var applied = await extras.ApplyAsync(read with { Rollups = [], NeedsYou = null }, CancellationToken.None);

        applied.NeedsYou.ShouldBeNull();
        applied.Rollups.ShouldBe([new RollupCountDto(WorkItemTypeId.Story, 1, 0, 0, 0)]);
        counts.Calls.ShouldBeEmpty();
    }

    private sealed class NoUser : ICurrentUser
    {
        public Guid Id => throw new InvalidOperationException("No signed-in user.");
    }
}
