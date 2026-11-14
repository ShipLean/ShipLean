using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Plans;

namespace ShipLean.WebApi.Tests.Timeline;

// The facts reader (C3) against a real database. The database is shared by the class, so every test arranges its own Feature.
[TestFixture]
public class TimelineFactsReaderTests : ApiFixture
{
    private static readonly Guid Ship = SeedIds.Projects.Ship;
    private static readonly DateTimeOffset T0 = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AsOf = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private Task<TimelineFacts> ReadAsync(Guid featureId, DateTimeOffset? asOf = null)
        => Host.Services.GetRequiredService<ITimelineFactsReader>().ReadAsync(Ship, featureId, asOf ?? AsOf, CancellationToken.None);

    private async Task<(Guid Id, string Key)> ItemAsync(string type, Guid? parent = null, Guid? state = null, string[]? tags = null)
    {
        var builder = new WorkItemBuilder(Ship).WithType(type).WithCreatedAt(T0).WithCreatedBy(SeedIds.Users.Joel);
        if (parent is { } parentId)
        {
            builder.WithParent(parentId);
        }
        if (state is { } stateId)
        {
            builder.WithState(stateId);
        }
        if (tags is not null)
        {
            builder.WithTags(tags);
        }
        await using var connection = await OpenDatabaseAsync();
        var id = await builder.InsertAsync(connection);
        // The builder stamps revision 1 with now(); pin it to the creation time so the history is deterministic.
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @at WHERE work_item_id = @id AND rev = 1", new { id, at = T0 });
        return (id, await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }) ?? throw new InvalidOperationException());
    }

    private async Task<(Guid Feature, Guid Story, Guid Task, string TaskKey)> TreeAsync()
    {
        var feature = await ItemAsync("feature");
        var story = await ItemAsync("story", feature.Id);
        var task = await ItemAsync("task", story.Id);
        return (feature.Id, story.Id, task.Id, task.Key);
    }

    private async Task MoveAsync(Guid id, int rev, DateTimeOffset at, Guid state, string? agent = null)
    {
        await using var connection = await OpenDatabaseAsync();
        var revision = new RevisionBuilder(id).WithRev(rev).WithChangedAt(at).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"state","from":null,"to":"{{state}}"}]""");
        await (agent is null ? revision : revision.WithAgent(agent)).InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @state, revision = @rev WHERE id = @id", new { id, state, rev });
    }

    private async Task TagChangeAsync(Guid id, int rev, DateTimeOffset at, string from, string to)
    {
        await using var connection = await OpenDatabaseAsync();
        await new RevisionBuilder(id).WithRev(rev).WithChangedAt(at).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"tags","from":{{from}},"to":{{to}}}]""").InsertAsync(connection);
    }

    private async Task LinkAsync(Guid source, Guid target, string type)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO work_item_link (id, source_id, target_id, link_type) VALUES (@id, @source, @target, @type)",
            new { id = Guid.NewGuid(), source, target, type });
    }

    private async Task ReviewAsync(Guid id, DateTimeOffset at, string verdict, string? note, string? agent)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO review_event (id, work_item_id, verdict, note, by_user_id, agent_name, at) VALUES (@eventId, @id, @verdict, @note, @by, @agent, @at)",
            new { eventId = Guid.NewGuid(), id, verdict, note, by = SeedIds.Users.Joel, agent, at });
    }

    private async Task SprintAsync(string name, string start, string end)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @Ship, @name, CAST(@start AS date), CAST(@end AS date))",
            new { id = Guid.NewGuid(), Ship, name, start, end });
    }

    [Test]
    public async Task Read_WhenTaskMovedThroughStates_ShouldReturnEveryMoveInOrder()
    {
        var tree = await TreeAsync();
        await MoveAsync(tree.Task, 2, T0.AddHours(1), SeedIds.States.Ship.TaskActive, "builder");
        await MoveAsync(tree.Task, 3, T0.AddHours(2), SeedIds.States.Ship.TaskInReview);
        await MoveAsync(tree.Task, 4, T0.AddHours(3), SeedIds.States.Ship.TaskActive);
        await MoveAsync(tree.Task, 5, T0.AddHours(4), SeedIds.States.Ship.TaskInReview);

        var facts = await ReadAsync(tree.Feature);

        var moves = facts.Moves.Where(move => move.ItemKey == tree.TaskKey).ToList();
        moves.Select(move => move.StateName).ShouldBe(["New", "Active", "In review", "Active", "In review"]);
        moves.Select(move => move.Rev).ShouldBe([1, 2, 3, 4, 5]);
        moves.Select(move => move.At).ShouldBe([T0, T0.AddHours(1), T0.AddHours(2), T0.AddHours(3), T0.AddHours(4)]);
        moves[1].AgentName.ShouldBe("builder");
        moves[1].Category.ShouldBe(StateCategory.InProgress);
        moves[1].UserId.ShouldBe(SeedIds.Users.Joel);
        moves[0].Category.ShouldBe(StateCategory.Proposed);
        facts.Moves.Select(move => move.ItemKey).Distinct().Count().ShouldBe(3);
    }

    [Test]
    public async Task Read_WhenBlockedTagAddedAndRemoved_ShouldReturnBothChanges()
    {
        var tree = await TreeAsync();
        await TagChangeAsync(tree.Task, 2, T0.AddHours(1), "[]", """["blocked"]""");
        await TagChangeAsync(tree.Task, 3, T0.AddHours(2), """["blocked"]""", "[]");
        await TagChangeAsync(tree.Task, 4, T0.AddHours(3), "[]", """["wave:1"]""");

        var facts = await ReadAsync(tree.Feature);

        facts.BlockedTags.Select(tag => (tag.ItemKey, tag.At, tag.Rev, tag.BlockedAfter))
            .ShouldBe([(tree.TaskKey, T0.AddHours(1), 2, true), (tree.TaskKey, T0.AddHours(2), 3, false)]);
    }

    [Test]
    public async Task Read_WhenSuccessorAndBlockedByLinks_ShouldNormaliseDirection()
    {
        var feature = await ItemAsync("feature");
        var story = await ItemAsync("story", feature.Id);
        var a = await ItemAsync("task", story.Id);
        var b = await ItemAsync("task", story.Id);
        var c = await ItemAsync("task", story.Id);
        var d = await ItemAsync("task", story.Id);
        await LinkAsync(a.Id, b.Id, "successor");
        await LinkAsync(b.Id, c.Id, "blockedBy");
        await LinkAsync(c.Id, d.Id, "predecessor");

        var facts = await ReadAsync(feature.Id);

        facts.Links.ShouldBe(
        [
            new TimelineLinkFact(a.Key, b.Key, TimelineLinkKind.Successor),
            new TimelineLinkFact(c.Key, b.Key, TimelineLinkKind.BlockedBy),
            new TimelineLinkFact(d.Key, c.Key, TimelineLinkKind.Predecessor),
        ]);
    }

    [Test]
    public async Task Read_WhenLinkToItemOutsideFeature_ShouldLeaveItOut()
    {
        var tree = await TreeAsync();
        var other = await TreeAsync();
        var inside = await ItemAsync("task", (await ReadStoryAsync(tree.Task)));
        await LinkAsync(tree.Task, other.Task, "successor");
        await LinkAsync(tree.Task, inside.Id, "successor");
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO work_item_link (id, source_id, target_id, link_type, archived_at) VALUES (@id, @a, @b, 'successor', now())",
                new { id = Guid.NewGuid(), a = inside.Id, b = tree.Task });
        }

        var facts = await ReadAsync(tree.Feature);

        facts.Links.Select(link => (link.From, link.To)).ShouldBe([(tree.TaskKey, inside.Key)]);
    }

    private async Task<Guid> ReadStoryAsync(Guid task)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<Guid>("SELECT parent_id FROM work_item WHERE id = @task", new { task });
    }

    [Test]
    public async Task Read_WhenPlanCaptured_ShouldReturnWavesAndPlannedAt()
    {
        Host.ActAs(SeedIds.Users.Joel);
        var keys = await PlanTreeSeed.SeedAsync(Host.Services, Ship, PlanFixture.FeatureEditor);
        var featureId = keys.Ids[keys.Feature];
        var first = T0;
        var second = T0.AddDays(2);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO mcp_capture_plan_apply (plan_id, project_id, feature_id, applied_at) VALUES (@a, @Ship, @featureId, @first), (@b, @Ship, @featureId, @second)",
                new { a = Guid.NewGuid(), b = Guid.NewGuid(), Ship, featureId, first, second });
        }

        var facts = await ReadAsync(featureId);

        facts.PlanAppliedAt.ShouldBe(second);
        facts.Waves.Count.ShouldBe(PlanFixture.FeatureEditor.Stories.Count);
        facts.Waves.Values.Distinct().Order().ShouldBe(["0.1", "0.2", "1", "2"]);
        var story = PlanFixture.FeatureEditor.Stories[0];
        facts.Waves[keys.Stories[story.Title]].ShouldBe(story.Wave.Text);
        facts.Tasks.Count.ShouldBe(PlanFixture.FeatureEditor.Stories.Sum(s => s.Tasks.Count));
    }

    [Test]
    public async Task Read_WhenNoPlanApplied_ShouldReturnNullPlannedAt()
    {
        var tree = await TreeAsync();

        (await ReadAsync(tree.Feature)).PlanAppliedAt.ShouldBeNull();
    }

    [Test]
    public async Task Read_WhenReviewEventsLogged_ShouldReturnThem()
    {
        var tree = await TreeAsync();
        await ReviewAsync(tree.Task, T0.AddHours(5), "changesRequested", "Rename it", "reviewer");
        await ReviewAsync(tree.Task, T0.AddHours(3), "approved", null, null);

        var facts = await ReadAsync(tree.Feature);

        facts.Reviews.Select(review => (review.ItemKey, review.At, review.Verdict, review.Note, review.ByUserId, review.AgentName)).ShouldBe(
        [
            (tree.TaskKey, T0.AddHours(3), ReviewVerdict.Approved, null, SeedIds.Users.Joel, null),
            (tree.TaskKey, T0.AddHours(5), ReviewVerdict.ChangesRequested, "Rename it", SeedIds.Users.Joel, "reviewer"),
        ]);
        facts.People[SeedIds.Users.Joel].ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task Read_WhenChildRemoved_ShouldLeaveItOut()
    {
        var feature = await ItemAsync("feature");
        var keptStory = await ItemAsync("story", feature.Id);
        var removedStory = await ItemAsync("story", feature.Id, SeedIds.States.Ship.StoryRemoved);
        var keptTask = await ItemAsync("task", keptStory.Id);
        var removedTask = await ItemAsync("task", keptStory.Id, SeedIds.States.Ship.TaskRemoved);
        var orphanedTask = await ItemAsync("task", removedStory.Id);

        var facts = await ReadAsync(feature.Id);

        facts.Stories.Select(story => story.Key).ShouldBe([keptStory.Key]);
        facts.Tasks.Select(task => task.Key).ShouldBe([keptTask.Key]);
        facts.Moves.Select(move => move.ItemKey).ShouldNotContain(removedTask.Key);
        facts.Moves.Select(move => move.ItemKey).ShouldNotContain(removedStory.Key);
        facts.Moves.Select(move => move.ItemKey).ShouldNotContain(orphanedTask.Key);
    }

    [Test]
    public async Task Read_WhenSprintsBeforeDuringAndAfter_ShouldReturnOnlyOverlapping()
    {
        // The task moves on 2026-10-05 (T0) and asOf is 2026-11-01, so the window is those two days, ends included.
        var tree = await TreeAsync();
        var project = await ProjectSprintSuffixAsync();
        await SprintAsync($"before-{project}", "2026-09-21", "2026-10-04");
        await SprintAsync($"edge-start-{project}", "2026-09-28", "2026-10-05");
        await SprintAsync($"during-{project}", "2026-10-12", "2026-10-25");
        await SprintAsync($"edge-end-{project}", "2026-11-01", "2026-11-14");
        await SprintAsync($"after-{project}", "2026-11-02", "2026-11-15");

        var facts = await ReadAsync(tree.Feature);

        facts.Sprints.Select(sprint => sprint.Name).Where(name => name.EndsWith(project, StringComparison.Ordinal))
            .ShouldBe([$"edge-start-{project}", $"during-{project}", $"edge-end-{project}"]);
        facts.Sprints.Single(sprint => sprint.Name == $"during-{project}").StartDate.ShouldBe(new DateOnly(2026, 10, 12));
        facts.Sprints.Single(sprint => sprint.Name == $"during-{project}").EndDate.ShouldBe(new DateOnly(2026, 10, 25));
    }

    // Sprint names are unique per project, and the seed has sprints of its own that overlap these dates.
    private static Task<string> ProjectSprintSuffixAsync() => Task.FromResult(Guid.NewGuid().ToString("N")[..6]);

    [Test]
    public async Task Read_WhenNoTaskMoved_ShouldReturnNoSprints()
    {
        var feature = await ItemAsync("feature");
        await ItemAsync("story", feature.Id);

        (await ReadAsync(feature.Id)).Sprints.ShouldBeEmpty();
    }

    [Test]
    public async Task Read_ShouldReturnCurrentStatesAndProjectKey()
    {
        var tree = await TreeAsync();
        await MoveAsync(tree.Task, 2, T0.AddHours(1), SeedIds.States.Ship.TaskInReview);

        var facts = await ReadAsync(tree.Feature);

        facts.ProjectKey.ShouldBe("SHIP");
        facts.Feature.Id.ShouldBe(tree.Feature);
        facts.Feature.StateName.ShouldBe("New");
        facts.Feature.StateCategory.ShouldBe(StateCategory.Proposed);
        facts.Feature.CreatedAt.ShouldBe(T0);
        facts.Feature.CreatedBy.ShouldBe(SeedIds.Users.Joel);
        facts.Feature.Epic.ShouldBeNull();
        var story = facts.Stories.Single();
        story.Type.ShouldBe(WorkItemTypeId.Story);
        story.StateName.ShouldBe("New");
        var task = facts.Tasks.Single();
        task.StateName.ShouldBe("In review");
        task.StateCategory.ShouldBe(StateCategory.Resolved);
        task.ParentKey.ShouldBe(story.Key);
    }

    [Test]
    public async Task Read_ShouldNotReadAnotherFeaturesItems()
    {
        var mine = await TreeAsync();
        var other = await TreeAsync();
        await MoveAsync(other.Task, 2, T0.AddHours(1), SeedIds.States.Ship.TaskActive);
        await TagChangeAsync(other.Task, 3, T0.AddHours(2), "[]", """["Blocked"]""");
        await ReviewAsync(other.Task, T0.AddHours(3), "approved", null, null);

        var facts = await ReadAsync(mine.Feature);

        facts.Tasks.Select(task => task.Key).ShouldBe([mine.TaskKey]);
        facts.Moves.Select(move => move.ItemKey).Distinct().ShouldNotContain(other.TaskKey);
        facts.Moves.Count.ShouldBe(3);
        facts.BlockedTags.ShouldBeEmpty();
        facts.Reviews.ShouldBeEmpty();
    }
}
