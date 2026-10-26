using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Plans;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Status;

// plan_status against a seeded Feature Editor plan, as Joel. Each test seeds its own plan, because they move its states.
[TestFixture]
public class PlanStatusTests : McpFixture
{
    private PlanSeedKeys _keys = null!;

    [SetUp]
    public async Task SeedPlanAsync()
    {
        Host.ActAs(SeedIds.Users.Joel);
        _keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
    }

    private async Task<JsonElement> StatusAsync(string item)
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var reply = await CallToolAsync(token, "plan_status", $$"""{"item":"{{item}}"}""");
        reply.IsToolError().ShouldBeFalse();
        return reply.Structured();
    }

    private static IEnumerable<JsonElement> TasksOf(JsonElement status)
        => status.GetProperty("waves").EnumerateArray().SelectMany(wave => wave.GetProperty("stories").EnumerateArray())
            .SelectMany(story => story.GetProperty("tasks").EnumerateArray());

    private async Task MoveAsync(string key, Guid state, Guid? assignee = null)
    {
        Host.ActAs(SeedIds.Users.Joel);
        using var scope = Host.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorkItemStore>();
        var id = _keys.Ids[key];
        var current = await store.GetAsync(id);
        var outcome = await store.PatchAsync(id, new WorkItemPatch(current.Revision, new WorkItemChanges { State = new FieldSet<Guid>(state), Assignee = assignee is null ? null : new FieldSet<Guid?>(assignee) }), WriteOptions.User);
        outcome.Written.ShouldNotBeNull();
    }

    [Test]
    public async Task PlanStatus_ShouldReturnWavesStoriesTasksInOrder()
    {
        var plan = PlanFixture.FeatureEditor;

        var status = await StatusAsync(_keys.Feature);

        status.GetProperty("feature").GetString().ShouldBe(_keys.Feature);
        var waves = status.GetProperty("waves").EnumerateArray().ToList();
        waves.Select(wave => wave.GetProperty("wave").GetString()).ShouldBe(["0.1", "0.2", "1", "2"]);
        waves.ShouldAllBe(wave => !wave.GetProperty("done").GetBoolean());
        status.GetProperty("startable_wave").GetString().ShouldBe("0.1");
        TasksOf(status).Count().ShouldBe(plan.Stories.Sum(story => story.Tasks.Count));

        var lane = waves.SelectMany(wave => wave.GetProperty("stories").EnumerateArray()).First(story => story.GetProperty("lane").GetString() == "F");
        var fixtureLane = plan.Stories.First(story => story.Lane == "F");
        lane.GetProperty("key").GetString().ShouldBe(_keys.Stories[fixtureLane.Title]);
        var tasks = lane.GetProperty("tasks").EnumerateArray().ToList();
        tasks.Select(task => task.GetProperty("title").GetString()).ShouldBe(fixtureLane.Tasks.Select(task => task.Title));
        var first = tasks[0];
        first.GetProperty("model").GetString().ShouldBe(fixtureLane.Tasks[0].Model);
        first.GetProperty("context_k").GetInt32().ShouldBe(fixtureLane.Tasks[0].ContextK);
        first.GetProperty("category").GetString().ShouldBe("proposed");
        first.GetProperty("state").GetString().ShouldBe("New");
        first.GetProperty("assignee").ValueKind.ShouldBe(JsonValueKind.Null);
        lane.GetProperty("state").GetString().ShouldBe("New");
        lane.GetProperty("assignee").ValueKind.ShouldBe(JsonValueKind.Null);
        first.GetProperty("started_at").ValueKind.ShouldBe(JsonValueKind.Null);
        first.GetProperty("finished_at").ValueKind.ShouldBe(JsonValueKind.Null);
        first.GetProperty("commits").GetArrayLength().ShouldBe(0);
        first.GetProperty("review_rounds").GetInt32().ShouldBe(0);
        first.GetProperty("orphan").GetBoolean().ShouldBeFalse();
    }

    private async Task<List<DateTimeOffset>> RevisionTimesAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return (await connection.QueryAsync<DateTimeOffset>(
            "SELECT changed_at FROM revision WHERE work_item_id = @id ORDER BY rev", new { id = _keys.Ids[key] })).ToList();
    }

    [Test]
    public async Task PlanStatus_ShouldTakeStartAndEndFromHistory()
    {
        var title = PlanFixture.FeatureEditor.Stories[0].Tasks[0].Title;
        var key = _keys.Tasks[title];
        await MoveAsync(key, SeedIds.States.Ship.TaskActive, SeedIds.Users.Joel);
        await MoveAsync(key, SeedIds.States.Ship.TaskInReview);
        await MoveAsync(key, SeedIds.States.Ship.TaskActive);
        await MoveAsync(key, SeedIds.States.Ship.TaskClosed);

        var status = await StatusAsync(_keys.Feature);

        var times = await RevisionTimesAsync(key);
        times.Count.ShouldBe(5);
        var task = TasksOf(status).Single(candidate => candidate.GetProperty("title").GetString() == title);
        task.GetProperty("category").GetString().ShouldBe("completed");
        task.GetProperty("state").GetString().ShouldBe("Closed");
        task.GetProperty("assignee").GetString().ShouldNotBeNullOrEmpty();
        task.GetProperty("started_at").GetDateTimeOffset().ShouldBe(times[1]);
        task.GetProperty("finished_at").GetDateTimeOffset().ShouldBe(times[4]);
        var story = status.GetProperty("waves").EnumerateArray().SelectMany(wave => wave.GetProperty("stories").EnumerateArray())
            .Single(candidate => candidate.GetProperty("tasks").EnumerateArray().Any(t => t.GetProperty("title").GetString() == title));
        story.GetProperty("assignee").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task PlanStatus_ReopenedTask_ShouldReportLastCloseAsFinished()
    {
        var title = PlanFixture.FeatureEditor.Stories[0].Tasks[0].Title;
        var key = _keys.Tasks[title];
        await MoveAsync(key, SeedIds.States.Ship.TaskActive);
        await MoveAsync(key, SeedIds.States.Ship.TaskClosed);
        await MoveAsync(key, SeedIds.States.Ship.TaskActive);

        var reopened = TasksOf(await StatusAsync(_keys.Feature)).Single(candidate => candidate.GetProperty("title").GetString() == title);
        reopened.GetProperty("category").GetString().ShouldBe("inProgress");
        reopened.GetProperty("finished_at").ValueKind.ShouldBe(JsonValueKind.Null);

        await MoveAsync(key, SeedIds.States.Ship.TaskClosed);

        var closed = TasksOf(await StatusAsync(_keys.Feature)).Single(candidate => candidate.GetProperty("title").GetString() == title);
        var times = await RevisionTimesAsync(key);
        times.Count.ShouldBe(5);
        closed.GetProperty("started_at").GetDateTimeOffset().ShouldBe(times[1]);
        closed.GetProperty("finished_at").GetDateTimeOffset().ShouldBe(times[4]);
    }

    [Test]
    public async Task PlanStatus_ShouldCountReviewCommentsAndListCommitLinks()
    {
        var title = PlanFixture.FeatureEditor.Stories[0].Tasks[0].Title;
        var id = _keys.Ids[_keys.Tasks[title]];
        var other = _keys.Ids[_keys.Tasks[PlanFixture.FeatureEditor.Stories[0].Tasks[1].Title]];
        Host.ActAs(SeedIds.Users.Joel);
        using (var scope = Host.Services.CreateScope())
        {
            var links = scope.ServiceProvider.GetRequiredService<ILinkCards>();
            await links.AddUrlAsync(id, "https://git.example.test/repo/commit/aaaaaaa1", PlanMarkers.Commit("aaaaaaa1234", "First change"), null, null);
            await links.AddUrlAsync(id, "https://git.example.test/repo/pull/3", PlanMarkers.PullRequest("Add the goal field"), null, null);
            await links.AddUrlAsync(id, "https://git.example.test/repo/commit/bbbbbbb2", PlanMarkers.Commit("bbbbbbb2345", "Second change"), null, null);
            await links.AddUrlAsync(id, "https://docs.example.test/design", "Design notes", null, null);
            var withdrawn = await links.AddUrlAsync(id, "https://git.example.test/repo/commit/ddddddd4", PlanMarkers.Commit("ddddddd4567", "Withdrawn change"), null, null);
            await links.ArchiveAsync(withdrawn.Id);
            await links.AddUrlAsync(other, "https://git.example.test/repo/commit/ccccccc3", PlanMarkers.Commit("ccccccc3456", "Elsewhere"), null, null);

            var comments = scope.ServiceProvider.GetRequiredService<ICommentStore>();
            await comments.AddAsync(id, new CommentCreate(PlanMarkers.Review("Round one: rename the field.")));
            var second = await comments.AddAsync(id, new CommentCreate(PlanMarkers.Review("Round two: add the test.")));
            await comments.SupersedeAsync(second.Id);
            var archived = await comments.AddAsync(id, new CommentCreate(PlanMarkers.Review("Withdrawn.")));
            await comments.ArchiveAsync(archived.Id);
            await comments.AddAsync(id, new CommentCreate("Just a note."));
            await comments.AddAsync(id, new CommentCreate(PlanMarkers.Report("Done.")));
        }

        var status = await StatusAsync(_keys.Feature);

        var task = TasksOf(status).Single(candidate => candidate.GetProperty("title").GetString() == title);
        task.GetProperty("review_rounds").GetInt32().ShouldBe(2);
        var commits = task.GetProperty("commits").EnumerateArray().ToList();
        commits.Select(commit => commit.GetProperty("sha").GetString()).ShouldBe(["aaaaaaa", "bbbbbbb"]);
        commits.Select(commit => commit.GetProperty("subject").GetString()).ShouldBe(["First change", "Second change"]);
        commits.Select(commit => commit.GetProperty("url").GetString())
            .ShouldBe(["https://git.example.test/repo/commit/aaaaaaa1", "https://git.example.test/repo/commit/bbbbbbb2"]);
    }

    [Test]
    public async Task PlanStatus_NextWave_ShouldStartOnlyWhenEveryStoryInWaveIsDone()
    {
        var firstWave = PlanFixture.FeatureEditor.Stories.Where(story => story.Wave.Text == "0.1").ToList();
        foreach (var story in firstWave.SkipLast(1))
        {
            await MoveAsync(_keys.Stories[story.Title], SeedIds.States.Ship.StoryClosed);
        }

        var partial = await StatusAsync(_keys.Feature);
        partial.GetProperty("startable_wave").GetString().ShouldBe("0.1");

        await MoveAsync(_keys.Stories[firstWave[^1].Title], SeedIds.States.Ship.StoryClosed);

        var done = await StatusAsync(_keys.Feature);
        done.GetProperty("waves")[0].GetProperty("done").GetBoolean().ShouldBeTrue();
        done.GetProperty("startable_wave").GetString().ShouldBe("0.2");
    }

    [Test]
    public async Task PlanStatus_WhenStoriesHaveNoWaveTag_ShouldListEachAsAWave()
    {
        string feature;
        await using (var connection = await OpenDatabaseAsync())
        {
            var featureId = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Planned by hand").InsertAsync(connection);
            await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Done first").WithParent(featureId).WithBacklogOrder("x").WithState(SeedIds.States.Ship.StoryClosed).InsertAsync(connection);
            var next = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Then this").WithParent(featureId).WithBacklogOrder("y").InsertAsync(connection);
            await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithTitle("Its task").WithParent(next).InsertAsync(connection);
            feature = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @featureId", new { featureId });
        }

        var status = await StatusAsync(feature);

        var waves = status.GetProperty("waves").EnumerateArray().ToList();
        waves.Select(wave => wave.GetProperty("wave").GetString()).ShouldBe(["1", "2"]);
        waves.Select(wave => wave.GetProperty("stories")[0].GetProperty("title").GetString()).ShouldBe(["Done first", "Then this"]);
        status.GetProperty("startable_wave").GetString().ShouldBe("2");
        TasksOf(status).Select(task => task.GetProperty("title").GetString()).ShouldBe(["Its task"]);
    }

    [Test]
    public async Task PlanStatus_OnATask_ShouldBeValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var key = _keys.Tasks[PlanFixture.FeatureEditor.Stories[0].Tasks[0].Title];

        var reply = await CallToolAsync(token, "plan_status", $$"""{"item":"{{key}}"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("validation");
        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("item");
    }
}
