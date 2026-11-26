using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Demos.Data;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;
using ShipLean.WebApi.Tests.Retros;

namespace ShipLean.WebApi.Tests.Demos;

// K7: the real ISprintDemoData over rows arranged straight into the test database. Every test has a project and a closed sprint of its own.
[TestFixture]
public class SprintDemoDataTests : ApiFixture
{
    private static readonly Guid Joel = SeedIds.Users.Joel;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset T0 = new(2026, 2, 17, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Features_ShouldBeTheSprintsFeatureParents()
    {
        await using var world = await WorldAsync();
        var checkout = await world.FeatureAsync("Checkout", description: "## Why\n\nShoppers drop out.\n\n- **Measure (to confirm):** Completion rate.\n\n## Plan\n\nx", goal: "One step");
        var coupons = await world.FeatureAsync("Coupons");
        var elsewhere = await world.FeatureAsync("Not in this sprint");
        await world.ItemAsync("story", parent: coupons, sprint: world.Sprint, title: "Coupon box");
        await world.ItemAsync("story", parent: checkout, sprint: world.Sprint, title: "Pay", points: 3);
        await world.ItemAsync("story", parent: elsewhere, title: "Unplanned");

        var data = await world.ReadAsync();

        data.Features.Select(f => f.Title).ShouldBe(["Checkout", "Coupons"]);
        var first = data.Features[0];
        first.Goal.ShouldBe("One step");
        first.Why.ShouldBe("Shoppers drop out.\n\n- **Measure (to confirm):** Completion rate.");
        first.Measure.ShouldBe("Completion rate. (to confirm)");
        first.State.ShouldBe("New");
        first.Url.ShouldBe($"/p/{world.Project.Key}/items/{data.Features[0].Key}");
        first.Stories.Select(s => (s.Title, s.Points)).ShouldBe([("Pay", (decimal?)3m)]);
        data.Sprint.ShouldBe(new SprintDemoSprint("Demo sprint", "Ship it", new DateOnly(2026, 2, 16), new DateOnly(2026, 3, 1), true));
        data.Truncated.ShouldBeFalse();
    }

    [Test]
    public async Task Features_WhenStoryRemoved_ShouldLeaveItOut()
    {
        await using var world = await WorldAsync();
        var onlyRemoved = await world.FeatureAsync("Only removed");
        var mixed = await world.FeatureAsync("Mixed");
        await world.ItemAsync("story", parent: onlyRemoved, sprint: world.Sprint, state: "Removed", title: "Gone");
        await world.ItemAsync("story", parent: mixed, sprint: world.Sprint, state: "Removed", title: "Also gone");
        await world.ItemAsync("story", parent: mixed, sprint: world.Sprint, title: "Kept");

        var data = await world.ReadAsync();

        data.Features.Select(f => f.Title).ShouldBe(["Mixed"]);
        data.Features[0].Stories.Select(s => s.Title).ShouldBe(["Kept"]);
    }

    [Test]
    public async Task Retro_WhenStoryOfFeature_ShouldCountForTheFeature()
    {
        await using var world = await WorldAsync();
        var feature = await world.FeatureAsync("F");
        var story = await world.ItemAsync("story", parent: feature, sprint: world.Sprint);
        var bug = await world.ItemAsync("bug", parent: feature, sprint: world.Sprint);
        var own = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: feature, SprintId: world.Sprint));
        var viaStory = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, SprintId: world.Sprint, AgentName: "a1"));
        var viaBug = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: bug, SprintId: world.Sprint, AgentName: "a2"));
        await world.Seed.PointAsync(own, new RetroSeedPoint(RetroCategory.WentWell, "Feature own point."));
        await world.Seed.PointAsync(viaStory, new RetroSeedPoint(RetroCategory.WentBadly, "Story point."));
        await world.Seed.PointAsync(viaBug, new RetroSeedPoint(RetroCategory.Improve, "Bug point."));

        var data = await world.ReadAsync();

        var retro = data.Features.Single().Retro;
        retro.WentWell.Select(p => p.Text).ShouldBe(["Feature own point."]);
        retro.WentBadly.Select(p => p.Text).ShouldBe(["Story point."]);
        retro.Improve.Select(p => p.Text).ShouldBe(["Bug point."]);
        data.SprintRetro.WentWell.ShouldBeEmpty();
        data.SprintRetro.WentBadly.ShouldBeEmpty();
    }

    [Test]
    public async Task Retro_WhenActionItemChild_ShouldNotCountForTheFeature()
    {
        await using var world = await WorldAsync();
        var feature = await world.FeatureAsync("F");
        await world.ItemAsync("story", parent: feature, sprint: world.Sprint);
        var action = await world.ItemAsync("action", parent: feature, sprint: world.Sprint);
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: action, SprintId: world.Sprint));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Action child point."));

        var data = await world.ReadAsync();

        data.Features.Single().Retro.Improve.ShouldBeEmpty();
        data.SprintRetro.Improve.Select(p => p.Text).ShouldBe(["Action child point."]);
    }

    [Test]
    public async Task SprintRetro_ShouldHoldPointsNotTiedToAShownFeature()
    {
        await using var world = await WorldAsync();
        var shown = await world.FeatureAsync("Shown");
        var hidden = await world.FeatureAsync("No story in the sprint");
        await world.ItemAsync("story", parent: shown, sprint: world.Sprint);
        var loose = await world.ItemAsync("story", sprint: world.Sprint);
        var underHidden = await world.ItemAsync("story", parent: hidden);
        var tied = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: shown, SprintId: world.Sprint));
        var sprintLevel = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Person, SprintId: world.Sprint));
        var looseRetro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: loose, SprintId: world.Sprint));
        var hiddenRetro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: underHidden, SprintId: world.Sprint));
        await world.Seed.PointAsync(tied, new RetroSeedPoint(RetroCategory.WentWell, "Tied to shown."));
        await world.Seed.PointAsync(sprintLevel, new RetroSeedPoint(RetroCategory.WentWell, "Whole sprint."));
        await world.Seed.PointAsync(looseRetro, new RetroSeedPoint(RetroCategory.WentWell, "Loose story."));
        await world.Seed.PointAsync(hiddenRetro, new RetroSeedPoint(RetroCategory.WentWell, "Feature not shown."));

        var data = await world.ReadAsync();

        data.Features.Single().Retro.WentWell.Select(p => p.Text).ShouldBe(["Tied to shown."]);
        data.SprintRetro.WentWell.Select(p => p.Text).ShouldBe(["Whole sprint.", "Loose story.", "Feature not shown."]);
    }

    [Test]
    public async Task NoFailureLogInDemoData_WhenLogHoldsASentinel_ShouldNotContainIt()
    {
        await using var world = await WorldAsync();
        var story = await world.StoryInFeatureAsync();
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, SprintId: world.Sprint));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Visible point."));
        await world.Seed.FailureAsync(retro, RetroFailureKind.Blocker, "SENTINEL-FAILURE-LOG-7731 stack trace");

        var json = await world.ReadJsonAsync();

        json.ShouldContain("Visible point.");
        json.ShouldNotContain("SENTINEL-FAILURE-LOG-7731");
    }

    [Test]
    public async Task NoRootCauseInDemoData_WhenPointHoldsASentinel_ShouldNotContainIt()
    {
        await using var world = await WorldAsync();
        var story = await world.StoryInFeatureAsync();
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, SprintId: world.Sprint));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Visible point.", RootCause: "SENTINEL-ROOT-CAUSE-5521"));

        var json = await world.ReadJsonAsync();

        json.ShouldContain("Visible point.");
        json.ShouldNotContain("SENTINEL-ROOT-CAUSE-5521");
    }

    [Test]
    public async Task NoReportCommentInDemoData_WhenCommentsHoldSentinels_ShouldNotContainThem()
    {
        await using var world = await WorldAsync();
        var feature = await world.FeatureAsync("F");
        var story = await world.ItemAsync("story", parent: feature, sprint: world.Sprint, title: "Visible story");
        var task = await world.ItemAsync("task", parent: story, title: "Visible task");
        await new CommentBuilder(task).WithBody("Report · done SENTINEL-REPORT-9042 with a raw log").InsertAsync(world.Connection);
        await new CommentBuilder(task).WithBody("Review · round 1 SENTINEL-REVIEW-3310").InsertAsync(world.Connection);
        await new CommentBuilder(story).WithBody("SENTINEL-STORY-COMMENT-6655").InsertAsync(world.Connection);

        var json = await world.ReadJsonAsync();

        json.ShouldContain("Visible task");
        json.ShouldNotContain("SENTINEL-REPORT-9042");
        json.ShouldNotContain("SENTINEL-REVIEW-3310");
        json.ShouldNotContain("SENTINEL-STORY-COMMENT-6655");
    }

    [Test]
    public async Task NoNotNeededReasonInDemoData_WhenPointHoldsASentinel_ShouldNotContainIt()
    {
        await using var world = await WorldAsync();
        var story = await world.StoryInFeatureAsync();
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, SprintId: world.Sprint));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Still a point.", NotNeededReason: "SENTINEL-NOT-NEEDED-8120"));

        var data = await world.ReadAsync();

        data.Features.Single().Retro.Improve.Select(p => p.Text).ShouldBe(["Still a point."]);
        Serialize(data).ShouldNotContain("SENTINEL-NOT-NEEDED-8120");
    }

    [Test]
    public async Task Stories_ShouldCarrySummaryCriteriaAndTaskTitles()
    {
        await using var world = await WorldAsync();
        var feature = await world.FeatureAsync("F");
        var story = await world.ItemAsync("story", parent: feature, sprint: world.Sprint, title: "Pay", description: "Checkout takes one step.", criteria: ["Pay on the page", "See a receipt"]);
        await world.ItemAsync("task", parent: story, title: "Build the step", state: "Closed");
        await world.ItemAsync("task", parent: story, title: "Write the tests");
        await world.ItemAsync("task", parent: story, title: "Removed task", state: "Removed");
        await world.ItemAsync("story", parent: feature, sprint: world.Sprint, title: "Bare");

        var line = (await world.ReadAsync()).Features.Single().Stories;

        line.Select(s => s.Title).ShouldBe(["Pay", "Bare"]);
        line[0].Summary.ShouldBe("Checkout takes one step.");
        line[0].Criteria.ShouldBe(["Pay on the page", "See a receipt"]);
        line[0].Tasks.ShouldBe([new SprintDemoTaskLine("Build the step", "Closed"), new SprintDemoTaskLine("Write the tests", "New")]);
        line[0].Type.ShouldBe("story");
        line[1].Summary.ShouldBeNull();
        line[1].Criteria.ShouldBeEmpty();
        line[1].Tasks.ShouldBeEmpty();
    }

    [Test]
    public async Task Retro_WhenOtherSprintsRetro_ShouldBeLeftOut()
    {
        await using var world = await WorldAsync();
        var feature = await world.FeatureAsync("F");
        var story = await world.ItemAsync("story", parent: feature, sprint: world.Sprint);
        var other = await RetroArrangement.SprintAsync(world.Connection, world.Project.Id, "Other sprint", new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 15));
        var here = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, SprintId: world.Sprint, AgentName: "here"));
        var there = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, SprintId: other, AgentName: "there"));
        var noSprint = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: feature, AgentName: "nowhere"));
        await world.Seed.PointAsync(here, new RetroSeedPoint(RetroCategory.WentWell, "This sprint."));
        await world.Seed.PointAsync(there, new RetroSeedPoint(RetroCategory.WentWell, "Another sprint."));
        await world.Seed.PointAsync(noSprint, new RetroSeedPoint(RetroCategory.WentWell, "No sprint."));

        var data = await world.ReadAsync();

        data.Features.Single().Retro.WentWell.Select(p => p.Text).ShouldBe(["This sprint."]);
        data.SprintRetro.WentWell.ShouldBeEmpty();
    }

    [Test]
    public async Task Retro_WhenThemesMerged_ShouldNameTheLiveTheme()
    {
        await using var world = await WorldAsync();
        var story = await world.StoryInFeatureAsync();
        var live = await world.Seed.ThemeAsync("Live theme");
        var merged = await world.Seed.MergedThemeAsync("Merged away", live);
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, SprintId: world.Sprint));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Repointed.", ThemeId: live));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "No theme."));
        await world.Connection.ExecuteAsync("UPDATE retro_point SET theme_id = @live WHERE theme_id = @merged", new { live, merged });

        var data = await world.ReadAsync();

        data.Features.Single().Retro.WentBadly.ShouldBe([new SprintDemoRetroPoint("Live theme", "Repointed."), new SprintDemoRetroPoint(null, "No theme.")]);
        Serialize(data).ShouldNotContain("Merged away");
    }

    [Test]
    public async Task ActionItems_WhenRemoved_ShouldBeLeftOut()
    {
        await using var world = await WorldAsync();
        var live = await world.ItemAsync("action", title: "Live action");
        var removed = await world.ItemAsync("action", title: "Removed action", state: "Removed");
        await world.Seed.ActionAsync(new RetroSeedAction(live, RaisedSprintId: world.Sprint));
        await world.Seed.ActionAsync(new RetroSeedAction(removed, RaisedSprintId: world.Sprint));

        var data = await world.ReadAsync();

        data.ActionItems.Select(a => a.Title).ShouldBe(["Live action"]);
    }

    [Test]
    public async Task Project_ShouldCarryTheEnvironmentAndNoBrand()
    {
        await using var world = await WorldAsync();
        await world.Connection.ExecuteAsync(
            "UPDATE project SET repo_url = 'https://github.com/example/shop', repo_base_branch = 'trunk' WHERE id = @id", new { id = world.Project.Id });
        await world.Connection.ExecuteAsync(
            "INSERT INTO project_demo_settings (project_id, environment, logo_blob, tokens) VALUES (@id, 'Run it with make dev.', 'SENTINEL-LOGO-BLOB', 'SENTINEL-TOKENS --x: 1;')", new { id = world.Project.Id });

        var data = await world.ReadAsync();

        data.Project.ShouldBe(new SprintDemoProject(world.Project.Key, $"Project {world.Project.Key}", "https://github.com/example/shop", "trunk", "Run it with make dev."));
        Serialize(data).ShouldNotContain("SENTINEL-LOGO-BLOB");
        Serialize(data).ShouldNotContain("SENTINEL-TOKENS");
    }

    [Test]
    public async Task Project_WhenNoSettingsRow_ShouldHaveAnEmptyEnvironment()
    {
        await using var world = await WorldAsync();

        (await world.ReadAsync()).Project.Environment.ShouldBe(string.Empty);
    }

    [Test]
    public async Task NoAuthorNamesInDemoData_WhenAgentNamesAreSentinels_ShouldNotContainThem()
    {
        await using var world = await WorldAsync();
        var story = await world.StoryInFeatureAsync();
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, SprintId: world.Sprint, AgentName: "SENTINEL-AGENT-4417"));
        await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, "Visible point.", AgentName: "SENTINEL-POINT-AGENT-4418", NotNeededReason: "r", DecidedAgentName: "SENTINEL-DECIDER-4419"));

        var json = await world.ReadJsonAsync();

        json.ShouldContain("Visible point.");
        json.ShouldNotContain("SENTINEL-AGENT-4417");
        json.ShouldNotContain("SENTINEL-POINT-AGENT-4418");
        json.ShouldNotContain("SENTINEL-DECIDER-4419");
        json.ShouldNotContain("subAgent");
        json.ShouldNotContain("conductor");
    }

    [Test]
    public async Task Retro_WhenNone_ShouldBeEmptyListsNotNull()
    {
        await using var world = await WorldAsync();
        await world.StoryInFeatureAsync();

        var data = await world.ReadAsync();

        foreach (var retro in new[] { data.Features.Single().Retro, data.SprintRetro })
        {
            retro.WentWell.ShouldNotBeNull().ShouldBeEmpty();
            retro.WentBadly.ShouldNotBeNull().ShouldBeEmpty();
            retro.Improve.ShouldNotBeNull().ShouldBeEmpty();
            retro.NotInPlan.ShouldNotBeNull().ShouldBeEmpty();
        }
        data.ActionItems.ShouldBeEmpty();
        data.OtherWork.ShouldBeEmpty();
        data.Features.Single().AttachmentNames.ShouldBeEmpty();
        data.Features.Single().PullRequests.ShouldBeEmpty();
    }

    [Test]
    public async Task ActionItems_ShouldListTheSprintsRetroActions()
    {
        await using var world = await WorldAsync();
        var other = await RetroArrangement.SprintAsync(world.Connection, world.Project.Id, "Other sprint", new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 15));
        var second = await world.ItemAsync("action", title: "Second");
        var first = await world.ItemAsync("action", title: "First", state: "Active");
        var elsewhere = await world.ItemAsync("action", title: "Raised elsewhere");
        var plain = await world.ItemAsync("action", title: "Not a retro action");
        await world.Seed.ActionAsync(new RetroSeedAction(first, RaisedSprintId: world.Sprint));
        await world.Seed.ActionAsync(new RetroSeedAction(second, RaisedSprintId: world.Sprint));
        await world.Seed.ActionAsync(new RetroSeedAction(elsewhere, RaisedSprintId: other));
        _ = plain;

        var data = await world.ReadAsync();

        data.ActionItems.Select(a => (a.Title, a.State)).ShouldBe([("Second", "New"), ("First", "Active")]);
        data.ActionItems[0].Key.ShouldStartWith(world.Project.Key);
    }

    [Test]
    public async Task OtherWork_ShouldExcludeActionItemsAndHiddenItems()
    {
        await using var world = await WorldAsync();
        var feature = await world.FeatureAsync("F");
        await world.ItemAsync("story", parent: feature, sprint: world.Sprint, title: "Under a feature");
        await world.ItemAsync("story", sprint: world.Sprint, title: "Loose story", points: 2);
        await world.ItemAsync("bug", sprint: world.Sprint, title: "Loose bug");
        await world.ItemAsync("action", sprint: world.Sprint, title: "An action item");
        await world.ItemAsync("bau", title: "A BAU item");
        await world.ItemAsync("story", sprint: world.Sprint, title: "Removed loose", state: "Removed");
        await world.ItemAsync("story", title: "Not in the sprint");

        var data = await world.ReadAsync();

        data.OtherWork.Select(s => (s.Title, s.Type)).ShouldBe([("Loose story", "story"), ("Loose bug", "bug")]);
        data.OtherWork[0].Points.ShouldBe(2m);
    }

    [Test]
    public async Task Plan_WhenFeatureHasNoPlan_ShouldBeNull()
    {
        await using var world = await WorldAsync();
        var noStories = await world.FeatureAsync("Only an action");
        var planned = await world.FeatureAsync("Planned");
        await world.ItemAsync("action", parent: noStories, sprint: world.Sprint);
        await world.ItemAsync("story", parent: planned, sprint: world.Sprint, tags: ["wave:1"]);

        var data = await world.ReadAsync();

        data.Features.Single(f => f.Title == "Only an action").Plan.ShouldBeNull();
        data.Features.Single(f => f.Title == "Only an action").Stories.ShouldBeEmpty();
        data.Features.Single(f => f.Title == "Planned").Plan.ShouldNotBeNull();
    }

    [Test]
    public async Task Plan_WhenCaptured_ShouldCountWavesTasksAndReviews()
    {
        await using var world = await WorldAsync();
        var feature = await world.FeatureAsync("F");
        var s1 = await world.ItemAsync("story", parent: feature, sprint: world.Sprint, tags: ["wave:1"]);
        var s2 = await world.ItemAsync("story", parent: feature, tags: ["wave:1", "lane:a"]);
        var s3 = await world.ItemAsync("story", parent: feature, tags: ["wave:2"]);
        await world.ItemAsync("story", parent: feature, tags: ["wave:1.5"], state: "Removed");
        await world.ItemAsync("story", parent: feature, title: "Hand made, no wave tag");
        var t1 = await world.ItemAsync("task", parent: s1);
        var t2 = await world.ItemAsync("task", parent: s2);
        await world.ItemAsync("task", parent: s3);
        await world.ItemAsync("task", parent: s3, state: "Removed");
        foreach (var body in new[] { "Review · round 1", "Review · round 2", "Report · done", "Not a Review · mid" })
        {
            await new CommentBuilder(t1).WithBody(body).InsertAsync(world.Connection);
        }
        await new CommentBuilder(t2).WithBody("Review · round 1").InsertAsync(world.Connection);
        await new CommentBuilder(t2).WithBody("Review · archived").Archived().InsertAsync(world.Connection);
        await world.LinkAsync(t1, "https://github.com/example/shop/pull/12", "PR https://github.com/example/shop/pull/12");
        await world.LinkAsync(t2, "https://github.com/example/shop/pull/12", "PR https://github.com/example/shop/pull/12");
        await world.LinkAsync(t2, "https://github.com/example/shop/commit/abc1234", "commit abc1234 subject");
        await world.LinkAsync(s2, "https://github.com/example/shop/pull/13", "PR https://github.com/example/shop/pull/13");
        await world.LinkAsync(t2, "https://github.com/example/shop/pull/99", "PR https://github.com/example/shop/pull/99", archived: true);

        var data = await world.ReadAsync();

        var plan = data.Features.Single().Plan.ShouldNotBeNull();
        plan.Waves.ShouldBe(3);
        plan.Stories.ShouldBe(4);
        plan.Tasks.ShouldBe(3);
        plan.ReviewRounds.ShouldBe(3);
        plan.PullRequests.ShouldBe(["https://github.com/example/shop/pull/12", "https://github.com/example/shop/pull/13"]);
        data.Features.Single().PullRequests.ShouldBe(plan.PullRequests);
    }

    // SHIP-2330: a wave runs from its earliest story start to its latest story end, read off the Feature's timeline in UTC; a story
    // with no wave tag is a wave of its own and stays open while it has not finished. Mutation: take the latest start instead of the
    // earliest in DemoTiming.Wave, or drop the untagged stories.
    [Test]
    public async Task PlanTiming_WhenWavesRan_ShouldTimeEachWaveInWaveOrder()
    {
        await using var world = await WorldAsync();
        var plan = await world.TimedPlanAsync();

        var data = await world.ReadAsync();

        var timing = data.Features.Single().Plan.ShouldNotBeNull().Timing.ShouldNotBeNull();
        timing.Waves.Select(w => (w.Label, string.Join(' ', w.StoryKeys))).ShouldBe(
            [("1", $"{plan.A} {plan.B}"), ("2", plan.C), (null, plan.Loose)]);
        timing.Waves[0].ShouldBe(timing.Waves[0] with { Start = T0, End = T0.AddHours(6), Hours = 6, DurationLabel = "6 h" });
        timing.Waves[1].ShouldBe(timing.Waves[1] with { Start = T0.AddHours(36), End = T0.AddHours(46), Hours = 10, DurationLabel = "10 h" });
        timing.Waves[2].ShouldBe(timing.Waves[2] with { Start = null, End = null, Hours = null, DurationLabel = null });
        timing.Execution.Start.ShouldBe(T0);
        timing.Execution.End.ShouldBeNull();
        timing.TimelinePath.ShouldBe($"/p/{world.Project.Key}/items/{data.Features.Single().Key}/timeline");
    }

    // SHIP-2330: the longest waits are the timeline's labelled edges (12 h or more) with both ends named, and the longest Blocked
    // periods come from the task bars. Mutation: keep edges under 12 h, or leave out the titles.
    [Test]
    public async Task PlanTiming_WhenWaitsAndBlocks_ShouldListTheLongest()
    {
        await using var world = await WorldAsync();
        var plan = await world.TimedPlanAsync();

        var timing = (await world.ReadAsync()).Features.Single().Plan.ShouldNotBeNull().Timing.ShouldNotBeNull();

        timing.LongestWaits.ShouldBe([new SprintDemoWait(plan.TaskB, "Task B", plan.TaskC, "Task C", "depends", 30, "1.3 days", false)]);
        timing.LongestBlocked.ShouldBe([new SprintDemoBlocked(plan.TaskB, "Task B", 1.5, "1.5 h")]);
    }

    [Test]
    public async Task Attachments_ShouldListTheFeaturesLiveNames()
    {
        await using var world = await WorldAsync();
        var feature = await world.FeatureAsync("F");
        await world.ItemAsync("story", parent: feature, sprint: world.Sprint);
        await world.AttachmentAsync(feature, "checkout-flow.png");
        await world.AttachmentAsync(feature, "old.png", archived: true);

        (await world.ReadAsync()).Features.Single().AttachmentNames.ShouldBe(["checkout-flow.png"]);
    }

    // D10: an agent session's run log is admin-only, and the demo data goes to an agent for whoever made the demo. Mutation: drop the run-log
    // filter from SprintDemoDataSql.Attachments.
    [Test]
    public async Task Attachments_ShouldLeaveOutAnAgentSessionsRunLog()
    {
        await using var world = await WorldAsync();
        var feature = await world.FeatureAsync("F");
        await world.ItemAsync("story", parent: feature, sprint: world.Sprint);
        await world.AttachmentAsync(feature, "checkout-flow.png");
        var runLog = await world.AttachmentAsync(feature, "agent-run-x.log");
        await new AgentSessionSeeds(feature, await AgentSessionSeeds.InsertConnectionAsync(world.Connection))
            .WithState("failed").WithRunLogAttachment(runLog).InsertAsync(world.Connection);

        (await world.ReadAsync()).Features.Single().AttachmentNames.ShouldBe(["checkout-flow.png"]);
    }

    [Test]
    public async Task Oversized_ShouldTruncateAndSetTheFlag()
    {
        await using var world = await WorldAsync();
        var feature = await world.FeatureAsync("Big", description: "## Why\n\n" + new string('w', 5000));
        for (var i = 0; i < 55; i++)
        {
            await world.ItemAsync("story", parent: feature, sprint: world.Sprint, title: $"Story {i:00}", description: i == 0 ? new string('s', 700) : "");
        }
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: feature, SprintId: world.Sprint));
        for (var i = 0; i < 45; i++)
        {
            await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, i == 0 ? new string('p', 500) : $"Point {i}"));
        }

        var data = await world.ReadAsync();

        var big = data.Features.Single();
        big.Why.ShouldNotBeNull().Length.ShouldBe(4096);
        big.Stories.Count.ShouldBe(50);
        big.StoriesOmitted.ShouldBe(5);
        big.Stories[0].Summary.ShouldNotBeNull().Length.ShouldBe(600);
        big.Retro.WentWell.Count.ShouldBe(40);
        big.Retro.WentWell[0].Text.Length.ShouldBe(400);
        data.Truncated.ShouldBeTrue();
    }

    [Test]
    public async Task Oversized_WhenNothingIsCut_ShouldNotSetTheFlag()
    {
        await using var world = await WorldAsync();
        var feature = await world.FeatureAsync("Exactly full", description: "## Why\n\n" + new string('w', 4096));
        for (var i = 0; i < 50; i++)
        {
            await world.ItemAsync("story", parent: feature, sprint: world.Sprint, description: new string('s', 600));
        }

        var data = await world.ReadAsync();

        data.Features.Single().Stories.Count.ShouldBe(50);
        data.Truncated.ShouldBeFalse();
    }

    [Test]
    public async Task Oversized_WhenStoryHasManyCriteria_ShouldKeepTenAndSetTheFlag()
    {
        await using var world = await WorldAsync();
        var feature = await world.FeatureAsync("F");
        await world.ItemAsync("story", parent: feature, sprint: world.Sprint, criteria: [.. Enumerable.Range(0, 12).Select(i => $"Criterion {i:00}")]);

        var data = await world.ReadAsync();

        data.Features.Single().Stories.Single().Criteria.Count.ShouldBe(10);
        data.Truncated.ShouldBeTrue();
    }

    [Test]
    public async Task Oversized_WhenStoryHasManyTasks_ShouldKeepThirtyAndSetTheFlag()
    {
        await using var world = await WorldAsync();
        var feature = await world.FeatureAsync("F");
        var story = await world.ItemAsync("story", parent: feature, sprint: world.Sprint);
        for (var i = 0; i < 35; i++)
        {
            await world.ItemAsync("task", parent: story, title: $"Task {i:00}");
        }

        var data = await world.ReadAsync();

        data.Features.Single().Stories.Single().Tasks.Count.ShouldBe(30);
        data.Truncated.ShouldBeTrue();
    }

    // SHIP-2330: summaries go before stories, so every Feature keeps its stories and only the later ones lose their summaries.
    // Before, the last Features were cut to a header with no stories. Mutation: drop stories before summaries in SprintDemoDataBudget.Trim.
    [Test]
    public async Task Oversized_WhenManyFeatures_ShouldKeepEveryFeaturesStoriesAndCutSummariesFromTheLast()
    {
        await using var world = await WorldAsync();
        for (var f = 0; f < 30; f++)
        {
            var feature = await world.FeatureAsync($"Feature {f:00}", goal: $"Goal {f:00}");
            for (var i = 0; i < 12; i++)
            {
                await world.ItemAsync("story", parent: feature, sprint: world.Sprint, title: $"Story {f:00}-{i:00}", description: new string('d', 600));
            }
        }

        var data = await world.ReadAsync();

        data.Features.Count.ShouldBe(30);
        SprintDemoDataBudget.MaxBytes.ShouldBe(120 * 1024);
        SprintDemoDataBudget.Size(data).ShouldBeLessThanOrEqualTo(120 * 1024);
        System.Text.Encoding.UTF8.GetByteCount(Serialize(data)).ShouldBeLessThanOrEqualTo(120 * 1024);
        data.Truncated.ShouldBeTrue();
        data.Features.ShouldAllBe(feature => feature.Stories.Count == 12 && feature.StoriesOmitted == 0 && feature.Goal.StartsWith("Goal "));
        var whole = data.Features.Count(feature => feature.Stories.All(story => story.Summary != null));
        whole.ShouldBeGreaterThan(0);
        whole.ShouldBeLessThan(30);
        data.Features.Take(whole).ShouldAllBe(feature => feature.Stories.All(story => story.Summary != null));
        data.Features.Skip(whole).ShouldAllBe(feature => feature.Stories.All(story => story.Summary == null));
        data.Features.Select(feature => feature.Title).ShouldBe(Enumerable.Range(0, 30).Select(f => $"Feature {f:00}"));
    }

    // SHIP-2330: a first Feature too big to keep its stories no longer stops the later ones getting theirs back. Mutation: stop the
    // give-back at the first Feature that doesn't fit.
    [Test]
    public async Task Oversized_WhenFirstFeatureIsTooBig_ShouldStillGiveLaterFeaturesTheirStories()
    {
        await using var world = await WorldAsync();
        var big = await world.FeatureAsync("A big one", description: "## Why\n\nBig reasons.");
        for (var i = 0; i < 40; i++)
        {
            await world.ItemAsync("story", parent: big, sprint: world.Sprint, title: $"Big {i:00} " + new string('b', 4000));
        }
        foreach (var name in new[] { "B small", "C small", "D small" })
        {
            var feature = await world.FeatureAsync(name);
            await world.ItemAsync("story", parent: feature, sprint: world.Sprint, title: $"{name} story 1", description: "Kept.");
            await world.ItemAsync("story", parent: feature, sprint: world.Sprint, title: $"{name} story 2");
        }

        var data = await world.ReadAsync();

        data.Truncated.ShouldBeTrue();
        SprintDemoDataBudget.Size(data).ShouldBeLessThanOrEqualTo(SprintDemoDataBudget.MaxBytes);
        var first = data.Features[0];
        first.Stories.ShouldBeEmpty();
        first.StoriesOmitted.ShouldBe(40);
        first.Why.ShouldBe("Big reasons.");
        data.Features.Skip(1).Select(feature => (feature.Title, feature.Stories.Count, feature.StoriesOmitted))
            .ShouldBe([("B small", 2, 0), ("C small", 2, 0), ("D small", 2, 0)]);
        data.Features[1].Stories[0].Summary.ShouldBe("Kept.");
    }

    [Test]
    public async Task OtherWork_WhenMoreThanFiftyLooseStories_ShouldKeepAllWithinBudget()
    {
        await using var world = await WorldAsync();
        for (var i = 0; i < 55; i++)
        {
            await world.ItemAsync("story", sprint: world.Sprint, title: $"Loose {i:00}");
        }

        var data = await world.ReadAsync();

        data.OtherWork.Count.ShouldBe(55);
        data.Truncated.ShouldBeFalse();
    }

    [Test]
    public async Task SprintRetro_WhenMoreThanFortyLoosePoints_ShouldKeepAllWithinBudget()
    {
        await using var world = await WorldAsync();
        var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, SprintId: world.Sprint));
        for (var i = 0; i < 45; i++)
        {
            await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, $"Sprint point {i:00}"));
        }

        var data = await world.ReadAsync();

        data.SprintRetro.WentWell.Count.ShouldBe(45);
        data.Truncated.ShouldBeFalse();
    }

    // SHIP-2330: a Feature whose stories don't fit keeps its header facts and its Why, and says how many stories were left out.
    [Test]
    public async Task Oversized_WhenFeatureLosesItsStories_ShouldKeepTheHeaderFactsAndCountTheOmitted()
    {
        await using var world = await WorldAsync();
        for (var f = 0; f < 30; f++)
        {
            var feature = await world.FeatureAsync($"Feature {f:00}", goal: $"Goal {f:00}", description: "## Why\n\nBecause.");
            var story = await world.ItemAsync("story", parent: feature, sprint: world.Sprint, tags: ["wave:1"], description: new string('d', 600));
            await world.LinkAsync(story, $"https://github.com/example/shop/pull/{f}", $"PR https://github.com/example/shop/pull/{f}");
            await world.AttachmentAsync(feature, $"shot-{f}.png");
            var retro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: feature, SprintId: world.Sprint));
            await world.Seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.WentWell, "Went well."));
            for (var i = 0; i < 11; i++)
            {
                await world.ItemAsync("story", parent: feature, sprint: world.Sprint, title: $"Story {f:00}-{i:00} " + new string('t', 400), description: new string('d', 600));
            }
        }

        var data = await world.ReadAsync();

        SprintDemoDataBudget.Size(data).ShouldBeLessThanOrEqualTo(SprintDemoDataBudget.MaxBytes);
        var shrunk = data.Features.Where(feature => feature.Stories.Count == 0).ToList();
        shrunk.ShouldNotBeEmpty();
        foreach (var header in shrunk)
        {
            var number = header.Title["Feature ".Length..];
            header.Goal.ShouldBe($"Goal {number}");
            header.Url.ShouldBe($"/p/{world.Project.Key}/items/{header.Key}");
            header.StoriesOmitted.ShouldBe(12);
            header.Why.ShouldBe("Because.");
            header.Retro.WentWell.ShouldBeEmpty();
            header.Retro.WentBadly.ShouldBeEmpty();
            header.Retro.Improve.ShouldBeEmpty();
            header.Retro.NotInPlan.ShouldBeEmpty();
            header.AttachmentNames.ShouldBeEmpty();
            header.PullRequests.ShouldBeEmpty();
            var plan = header.Plan.ShouldNotBeNull();
            plan.Stories.ShouldBe(12);
            var timing = plan.Timing.ShouldNotBeNull();
            timing.TimelinePath.ShouldBe($"/p/{world.Project.Key}/items/{header.Key}/timeline");
            timing.Waves.ShouldBeEmpty();
            timing.LongestWaits.ShouldBeEmpty();
            timing.LongestBlocked.ShouldBeEmpty();
            plan.PullRequests.ShouldBe([$"https://github.com/example/shop/pull/{int.Parse(number)}"]);
        }
    }

    [Test]
    public async Task Archived_PointsAndRetros_ShouldBeLeftOut()
    {
        await using var world = await WorldAsync();
        var story = await world.StoryInFeatureAsync();
        var live = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, SprintId: world.Sprint, AgentName: "live"));
        var archived = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, SprintId: world.Sprint, AgentName: "archived"));
        await world.Seed.PointAsync(live, new RetroSeedPoint(RetroCategory.WentWell, "Live point."));
        await world.Seed.PointAsync(live, new RetroSeedPoint(RetroCategory.WentWell, "Archived point.", Archived: true));
        await world.Seed.PointAsync(archived, new RetroSeedPoint(RetroCategory.WentWell, "Point of archived retro."));
        await world.Connection.ExecuteAsync("UPDATE retrospective SET archived_at = now() WHERE id = @archived", new { archived });

        var data = await world.ReadAsync();

        data.Features.Single().Retro.WentWell.Select(p => p.Text).ShouldBe(["Live point."]);
    }

    private static string Serialize(SprintDemoData data) => JsonSerializer.Serialize(data, Json);

    private async Task<World> WorldAsync()
    {
        var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection, (Joel, ProjectRole.Member));
        var sprint = await RetroArrangement.SprintAsync(connection, project.Id, "Demo sprint", new DateOnly(2026, 2, 16), new DateOnly(2026, 3, 1));
        await connection.ExecuteAsync("UPDATE sprint SET goal = 'Ship it', rollover_status = 'decided' WHERE id = @sprint", new { sprint });
        return new World(connection, project, sprint, Host.Services);
    }

    // The arranged project and sprint. It owns the connection until the fixture ends the scenario.
    private sealed class World(NpgsqlConnection connection, ArrangedProject project, Guid sprint, IServiceProvider services) : IAsyncDisposable
    {
        public NpgsqlConnection Connection { get; } = connection;

        public ArrangedProject Project { get; } = project;

        public Guid Sprint { get; } = sprint;

        public RetroSeed Seed { get; } = new(connection, project.Id);

        public ValueTask DisposeAsync() => Connection.DisposeAsync();

        public async Task<SprintDemoData> ReadAsync()
        {
            using var scope = services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISprintDemoData>().ReadAsync(Sprint);
        }

        public async Task<string> ReadJsonAsync() => Serialize(await ReadAsync());

        public async Task<Guid> FeatureAsync(string title, string? description = null, string? goal = null)
        {
            var id = await ItemAsync("feature", title: title, description: description);
            if (goal is not null)
            {
                await Connection.ExecuteAsync("UPDATE work_item SET goal = @goal WHERE id = @id", new { id, goal });
            }
            return id;
        }

        public async Task<Guid> StoryInFeatureAsync()
        {
            var feature = await FeatureAsync("F");
            return await ItemAsync("story", parent: feature, sprint: Sprint);
        }

        public async Task<Guid> ItemAsync(string type, Guid? parent = null, Guid? sprint = null, string? state = null, string? title = null, decimal? points = null, string? description = null, string[]? criteria = null, string[]? tags = null)
        {
            var builder = new WorkItemBuilder(Project.Id).WithType(type).WithTitle(title ?? $"{type} item");
            if (parent is { } parentId)
            {
                builder.WithParent(parentId);
            }
            if (sprint is { } sprintId)
            {
                builder.WithSprint(sprintId);
            }
            if (state is not null)
            {
                builder.WithStateNamed(state);
            }
            if (points is { } value)
            {
                builder.WithPoints(value);
            }
            if (tags is not null)
            {
                builder.WithTags(tags);
            }
            var id = await builder.InsertAsync(Connection);
            if (description is not null)
            {
                await Connection.ExecuteAsync("UPDATE work_item SET description = @description WHERE id = @id", new { id, description });
            }
            if (criteria is not null)
            {
                await Connection.ExecuteAsync(
                    "UPDATE work_item SET acceptance_criteria = @criteria::jsonb WHERE id = @id",
                    new { id, criteria = JsonSerializer.Serialize(criteria.Select(text => new { text, done = false })) });
            }
            return id;
        }

        // Wave 1 is stories A and B, wave 2 story C, and a loose story with no wave tag and no tasks. A's task runs T0 to +4 h, B's +1 h to
        // +6 h with a Blocked hour and a half, C's +36 h to +46 h: the wave edge from B's task to C's waits 30 h.
        public async Task<TimedPlan> TimedPlanAsync()
        {
            var feature = await FeatureAsync("Timed");
            var a = await ItemAsync("story", parent: feature, sprint: Sprint, tags: ["wave:1"]);
            var b = await ItemAsync("story", parent: feature, sprint: Sprint, tags: ["wave:1"]);
            var c = await ItemAsync("story", parent: feature, sprint: Sprint, tags: ["wave:2"]);
            var loose = await ItemAsync("story", parent: feature, sprint: Sprint);
            var taskA = await ItemAsync("task", parent: a, title: "Task A");
            var taskB = await ItemAsync("task", parent: b, title: "Task B");
            var taskC = await ItemAsync("task", parent: c, title: "Task C");
            foreach (var id in new[] { feature, a, b, c, loose, taskA, taskB, taskC })
            {
                await Connection.ExecuteAsync(
                    "UPDATE work_item SET created_at = @at WHERE id = @id; UPDATE revision SET changed_at = @at WHERE work_item_id = @id AND rev = 1",
                    new { id, at = T0.AddHours(-1) });
            }
            await MoveAsync(taskA, 2, T0, "Active");
            await MoveAsync(taskA, 3, T0.AddHours(4), "Resolved");
            await MoveAsync(taskB, 2, T0.AddHours(1), "Active");
            await TagsAsync(taskB, 3, T0.AddHours(2), "[]", """["Blocked"]""");
            await TagsAsync(taskB, 4, T0.AddHours(3.5), """["Blocked"]""", "[]");
            await MoveAsync(taskB, 5, T0.AddHours(6), "Resolved");
            await MoveAsync(taskC, 2, T0.AddHours(36), "Active");
            await MoveAsync(taskC, 3, T0.AddHours(46), "Resolved");
            return new TimedPlan(await KeyAsync(a), await KeyAsync(b), await KeyAsync(c), await KeyAsync(loose), await KeyAsync(taskB), await KeyAsync(taskC));
        }

        private async Task MoveAsync(Guid id, int rev, DateTimeOffset at, string stateName)
        {
            var state = Project.State(WorkItemTypeId.Task, stateName);
            await new RevisionBuilder(id).WithRev(rev).WithChangedAt(at).WithChangedBy(Joel)
                .WithChanges($$"""[{"field":"state","from":null,"to":"{{state}}"}]""").InsertAsync(Connection);
            await Connection.ExecuteAsync("UPDATE work_item SET state_id = @state, revision = @rev WHERE id = @id", new { id, state, rev });
        }

        private async Task TagsAsync(Guid id, int rev, DateTimeOffset at, string from, string to)
            => await new RevisionBuilder(id).WithRev(rev).WithChangedAt(at).WithChangedBy(Joel)
                .WithChanges($$"""[{"field":"tags","from":{{from}},"to":{{to}}}]""").InsertAsync(Connection);

        private async Task<string> KeyAsync(Guid id)
            => await Connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }) ?? throw new InvalidOperationException();

        public async Task LinkAsync(Guid itemId, string url, string title, bool archived = false)
            => await Connection.ExecuteAsync(
                "INSERT INTO work_item_link (id, source_id, link_type, url, title, archived_at) VALUES (@id, @itemId, 'url', @url, @title, CASE WHEN @archived THEN now() END)",
                new { id = Guid.NewGuid(), itemId, url, title, archived });

        public async Task<Guid> AttachmentAsync(Guid itemId, string name, bool archived = false)
        {
            var id = Guid.NewGuid();
            await Connection.ExecuteAsync(
                "INSERT INTO attachment (id, work_item_id, name, content_type, size_bytes, storage_ref, archived_at) VALUES (@id, @itemId, @name, 'image/png', 1, @ref, CASE WHEN @archived THEN now() END)",
                new { id, itemId, name, @ref = $"test/{Guid.NewGuid()}", archived });
            return id;
        }
    }

    private sealed record TimedPlan(string A, string B, string C, string Loose, string TaskB, string TaskC);
}
