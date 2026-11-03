using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Retros.Features;

// GET api/projects/{id}/retros/features, arranged in a project of the test's own.
public sealed class RetroFeaturesReaderTests : ApiFixture
{
    private static readonly Guid Joel = SeedIds.Users.Joel;

    private static readonly JsonSerializerOptions ResponseOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [Test]
    public async Task List_WhenFeatureHasStoryRetros_ShouldCountThemAll()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        var story = await world.ItemAsync("story", feature);
        await world.PointAsync(feature, RetroCategory.WentBadly, "Feature point.");
        await world.PointAsync(story, RetroCategory.Improve, "Story point.");
        await world.PointAsync(story, RetroCategory.NotInPlan, "Surprise.");
        await world.PointAsync(story, RetroCategory.WentWell, "Pairing.");
        var actionChild = await world.ItemAsync("action", feature);
        await world.PointAsync(actionChild, RetroCategory.WentBadly, "Not a story or bug.");

        var features = await world.ListAsync();

        var row = features.Features.Single();
        row.Item.Id.ShouldBe(feature);
        row.Retros.ShouldBe(4);
        row.OwnRetros.ShouldBe(1);
        row.Totals.Counts.ShouldBe(new RetroCategoryCountsDto(1, 1, 1, 1));
        row.Totals.Decidable.ShouldBe(3);
        row.Totals.ToDecide.ShouldBe(3);
        row.NoFeatureRetro.ShouldBeFalse();
        features.Loose.ShouldBeEmpty();
    }

    [Test]
    public async Task List_WhenOnlyStoryRetros_ShouldMarkNoFeatureRetro()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        var story = await world.ItemAsync("story", feature);
        await world.PointAsync(story, RetroCategory.Improve, "Story point.");

        var row = (await world.ListAsync()).Features.Single();

        row.Item.Id.ShouldBe(feature);
        row.OwnRetros.ShouldBe(0);
        row.Retros.ShouldBe(1);
        row.NoFeatureRetro.ShouldBeTrue();
    }

    [Test]
    public async Task List_WhenStoryHasNoFeature_ShouldBeLoose()
    {
        await using var world = await WorldAsync();
        var story = await world.ItemAsync("story");
        await world.PointAsync(story, RetroCategory.Improve, "Story point.");
        await world.ItemAsync("story");

        var features = await world.ListAsync();

        features.Features.ShouldBeEmpty();
        var row = features.Loose.Single();
        row.Item.Id.ShouldBe(story);
        row.Retros.ShouldBe(1);
        row.NoFeatureRetro.ShouldBeFalse();
    }

    [Test]
    public async Task List_WhenBugHasNoFeature_ShouldBeLoose()
    {
        await using var world = await WorldAsync();
        var bug = await world.ItemAsync("bug");
        await world.PointAsync(bug, RetroCategory.WentBadly, "Bug point.");

        var features = await world.ListAsync();

        features.Features.ShouldBeEmpty();
        features.Loose.Single().Item.Id.ShouldBe(bug);
        features.Loose.Single().Totals.Counts.WentBadly.ShouldBe(1);
    }

    [Test]
    public async Task List_WhenFeatureHasBugRetros_ShouldCountThem()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        var bug = await world.ItemAsync("bug", feature);
        await world.PointAsync(bug, RetroCategory.WentBadly, "Bug point one.");
        await world.PointAsync(bug, RetroCategory.WentBadly, "Bug point two.");

        var features = await world.ListAsync();

        var row = features.Features.Single();
        row.Item.Id.ShouldBe(feature);
        row.Retros.ShouldBe(2);
        row.Totals.Counts.WentBadly.ShouldBe(2);
        features.Loose.ShouldBeEmpty();
    }

    [Test]
    public async Task List_WhenActionCoversTwoPoints_ShouldCountOneAction()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        var story = await world.ItemAsync("story", feature);
        var first = await world.PointAsync(story, RetroCategory.Improve, "One.");
        var second = await world.PointAsync(story, RetroCategory.WentBadly, "Two.");
        await world.PointAsync(story, RetroCategory.Improve, "Three.");
        await world.ActionAsync([first, second]);

        var row = (await world.ListAsync()).Features.Single();

        row.Totals.Actions.ShouldBe(1);
        row.Totals.Decided.ShouldBe(2);
        row.Totals.ToDecide.ShouldBe(1);
        row.Totals.Decidable.ShouldBe(3);
    }

    [Test]
    public async Task List_WhenActionRemoved_ShouldCountPointsAsToDecide()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        var story = await world.ItemAsync("story", feature);
        var point = await world.PointAsync(story, RetroCategory.Improve, "One.");
        var action = await world.ActionAsync([point]);
        (await world.ListAsync()).Features.Single().Totals.Actions.ShouldBe(1);

        await world.Connection.ExecuteAsync(
            "UPDATE work_item SET state_id = (SELECT id FROM state WHERE project_id = @project AND type = 'action' AND category = 'removed' LIMIT 1) WHERE id = @action",
            new { project = world.Project.Id, action });
        var row = (await world.ListAsync()).Features.Single();

        row.Totals.Actions.ShouldBe(0);
        row.Totals.ToDecide.ShouldBe(1);
        row.Totals.Decided.ShouldBe(0);
    }

    [Test]
    public async Task List_ShouldNotCountWentWellAsDecidable()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        await world.PointAsync(feature, RetroCategory.WentWell, "Pairing.");
        await world.PointAsync(feature, RetroCategory.WentWell, "Reviews.");
        var notNeeded = await world.PointAsync(feature, RetroCategory.WentBadly, "Noise.");
        await world.Connection.ExecuteAsync("UPDATE retro_point SET not_needed_reason = 'Not a problem.', decided_by_user_id = @Joel, decided_at = now() WHERE id = @notNeeded", new { Joel, notNeeded });

        var totals = (await world.ListAsync()).Features.Single().Totals;

        totals.Counts.WentWell.ShouldBe(2);
        totals.Decidable.ShouldBe(1);
        totals.ToDecide.ShouldBe(0);
        totals.Decided.ShouldBe(1);
    }

    [Test]
    public async Task List_WhenRetroAndPointAreArchived_ShouldLeaveThemOut()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        var archivedRetro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.SubAgent, feature, AgentName: "gone"));
        await world.Seed.PointAsync(archivedRetro, new RetroSeedPoint(RetroCategory.Improve, "Gone."));
        await world.Connection.ExecuteAsync("UPDATE retrospective SET archived_at = now() WHERE id = @archivedRetro", new { archivedRetro });
        var live = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.SubAgent, feature, AgentName: "here"));
        await world.Seed.PointAsync(live, new RetroSeedPoint(RetroCategory.Improve, "Kept."));
        await world.Seed.PointAsync(live, new RetroSeedPoint(RetroCategory.Improve, "Archived point.", Archived: true));

        var row = (await world.ListAsync()).Features.Single();

        row.Retros.ShouldBe(1);
        row.Totals.Counts.Improve.ShouldBe(1);
    }

    [Test]
    public async Task List_WhenNoRetros_ShouldBeEmpty()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        await world.ItemAsync("story", feature);

        var features = await world.ListAsync();

        features.Features.ShouldBeEmpty();
        features.Loose.ShouldBeEmpty();
    }

    private async Task<World> WorldAsync()
    {
        var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection, (Joel, ProjectRole.Member));
        return new World(connection, project, Api);
    }

    private sealed record World(NpgsqlConnection Connection, ArrangedProject Project, ApiClient Api) : IAsyncDisposable
    {
        public RetroSeed Seed { get; } = new(Connection, Project.Id);

        public ValueTask DisposeAsync() => Connection.DisposeAsync();

        public async Task<Guid> ItemAsync(string type, Guid? parent = null)
        {
            var builder = new WorkItemBuilder(Project.Id).WithType(type);
            return await (parent is { } id ? builder.WithParent(id) : builder).InsertAsync(Connection);
        }

        // A point in a retro of its own, so one item can hold several retros.
        public async Task<Guid> PointAsync(Guid itemId, RetroCategory category, string text)
        {
            var retro = await Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.SubAgent, itemId, AgentName: $"agent-{Guid.NewGuid():N}"));
            return await Seed.PointAsync(retro, new RetroSeedPoint(category, text));
        }

        public async Task<Guid> ActionAsync(IReadOnlyList<Guid> pointIds)
        {
            var action = await ItemAsync("action");
            await Seed.ActionAsync(new RetroSeedAction(action, PointIds: pointIds));
            return action;
        }

        public async Task<RetroFeaturesDto> ListAsync()
        {
            using var response = await Api.GetAsync($"/api/projects/{Project.Id}/retros/features");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            return JsonSerializer.Deserialize<RetroFeaturesDto>(await response.Content.ReadAsStringAsync(), ResponseOptions)!;
        }
    }
}
