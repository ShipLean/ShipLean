using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Demos.Data;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;
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

    [Test]
    public async Task Oversized_WhenManyFeatures_ShouldKeepEveryFeatureAndShrinkTheLastToItsHeader()
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
        var whole = data.Features.Count(feature => feature.Stories.Count == 12);
        whole.ShouldBeGreaterThan(0);
        whole.ShouldBeLessThan(30);
        data.Features.Take(whole).ShouldAllBe(feature => feature.Stories.Count == 12);
        data.Features.Skip(whole).ShouldAllBe(feature => feature.Stories.Count == 0 && feature.Goal.StartsWith("Goal "));
        data.Features.Select(feature => feature.Title).ShouldBe(Enumerable.Range(0, 30).Select(f => $"Feature {f:00}"));
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

    [Test]
    public async Task Oversized_WhenFeatureShrinks_ShouldKeepOnlyTheHeaderFacts()
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
                await world.ItemAsync("story", parent: feature, sprint: world.Sprint, title: $"Story {f:00}-{i:00}", description: new string('d', 600));
            }
        }

        var data = await world.ReadAsync();

        var shrunk = data.Features.Where(feature => feature.Stories.Count == 0).ToList();
        shrunk.ShouldNotBeEmpty();
        foreach (var header in shrunk)
        {
            var number = header.Title["Feature ".Length..];
            header.Goal.ShouldBe($"Goal {number}");
            header.Url.ShouldBe($"/p/{world.Project.Key}/items/{header.Key}");
            header.Why.ShouldBeNull();
            header.Measure.ShouldBeNull();
            header.Retro.WentWell.ShouldBeEmpty();
            header.Retro.WentBadly.ShouldBeEmpty();
            header.Retro.Improve.ShouldBeEmpty();
            header.Retro.NotInPlan.ShouldBeEmpty();
            header.AttachmentNames.ShouldBeEmpty();
            header.PullRequests.ShouldBeEmpty();
            var plan = header.Plan.ShouldNotBeNull();
            plan.Stories.ShouldBe(12);
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

        public async Task LinkAsync(Guid itemId, string url, string title, bool archived = false)
            => await Connection.ExecuteAsync(
                "INSERT INTO work_item_link (id, source_id, link_type, url, title, archived_at) VALUES (@id, @itemId, 'url', @url, @title, CASE WHEN @archived THEN now() END)",
                new { id = Guid.NewGuid(), itemId, url, title, archived });

        public async Task AttachmentAsync(Guid itemId, string name, bool archived = false)
            => await Connection.ExecuteAsync(
                "INSERT INTO attachment (id, work_item_id, name, content_type, size_bytes, storage_ref, archived_at) VALUES (@id, @itemId, @name, 'image/png', 1, @ref, CASE WHEN @archived THEN now() END)",
                new { id = Guid.NewGuid(), itemId, name, @ref = $"test/{Guid.NewGuid()}", archived });
    }
}
