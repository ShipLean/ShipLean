using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;

namespace ShipLean.WebApi.Tests.Retros.Item;

// GET api/projects/{id}/retros/items/{key}, arranged in a project of the test's own.
public sealed class RetroItemReaderTests : ApiFixture
{
    private static readonly Guid Joel = SeedIds.Users.Joel;

    private static readonly JsonSerializerOptions ResponseOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [Test]
    public async Task Get_WhenFeature_ShouldListStoriesAndBugsAsChildren()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        var story = await world.ItemAsync("story", feature);
        var bug = await world.ItemAsync("bug", feature);
        await world.ItemAsync("task");
        await world.ItemAsync("story");
        await world.PointAsync(story, RetroCategory.Improve, "Slow.");

        var item = await world.GetAsync(feature);

        item.Item.Type.Id.ShouldBe(ShipLean.Contracts.Refs.WorkItemTypeId.Feature);
        item.Parent.ShouldBeNull();
        item.Children.Select(child => child.Item.Id).ShouldBe([story, bug]);
        item.Children[0].Retros.ShouldBe(1);
        item.Children[0].Totals.ToDecide.ShouldBe(1);
        item.Children[1].Retros.ShouldBe(0);
        item.Children[1].LastRetroLabel.ShouldBeNull();
    }

    [Test]
    public async Task Get_WhenStoryWithNoFeature_ShouldHaveNoParent()
    {
        await using var world = await WorldAsync();
        var story = await world.ItemAsync("story");
        await world.PointAsync(story, RetroCategory.Improve, "Story point.");

        var item = await world.GetAsync(story);

        item.Parent.ShouldBeNull();
        item.Children.ShouldBeEmpty();
        item.Cards.Count.ShouldBe(1);
        item.Totals.ToDecide.ShouldBe(1);
    }

    [Test]
    public async Task Get_WhenBugWithNoFeature_ShouldHaveNoParent()
    {
        await using var world = await WorldAsync();
        var bug = await world.ItemAsync("bug");
        await world.PointAsync(bug, RetroCategory.WentBadly, "Bug point.");

        var item = await world.GetAsync(bug);

        item.Parent.ShouldBeNull();
        item.Categories.SelectMany(column => column.Groups).SelectMany(group => group.Points).Select(point => point.Text).ShouldBe(["Bug point."]);
    }

    [Test]
    public async Task Get_WhenBugInAFeature_ShouldHaveTheFeatureAsParent()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        var bug = await world.ItemAsync("bug", feature);

        (await world.GetAsync(bug)).Parent.ShouldNotBeNull().Id.ShouldBe(feature);
    }

    [Test]
    public async Task Get_WhenTwoRetros_ShouldReturnBothCards()
    {
        await using var world = await WorldAsync();
        var story = await world.ItemAsync("story");
        await world.PointAsync(story, RetroCategory.Improve, "One.");
        await world.PointAsync(story, RetroCategory.WentWell, "Two.");

        var item = await world.GetAsync(story);

        item.Cards.Count.ShouldBe(2);
        item.Categories.Select(column => column.Category).ShouldBe([RetroCategory.WentWell, RetroCategory.NotInPlan, RetroCategory.WentBadly, RetroCategory.Improve]);
        item.Categories.SelectMany(column => column.Groups).SelectMany(group => group.Points).Select(point => point.Text).ShouldBe(["Two.", "One."], ignoreOrder: true);
        item.Totals.Decidable.ShouldBe(1);
    }

    [Test]
    public async Task Get_WhenFeature_ShouldCountChildrenInTotalsNotOnBoard()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        var story = await world.ItemAsync("story", feature);
        await world.PointAsync(feature, RetroCategory.WentBadly, "Feature point.");
        await world.PointAsync(story, RetroCategory.Improve, "Story point.");

        var item = await world.GetAsync(feature);

        item.Totals.Counts.WentBadly.ShouldBe(1);
        item.Totals.Counts.Improve.ShouldBe(1);
        item.Totals.ToDecide.ShouldBe(2);
        item.Cards.Count.ShouldBe(1);
        item.Categories.SelectMany(column => column.Groups).SelectMany(group => group.Points).Select(point => point.Text).ShouldBe(["Feature point."]);
    }

    [Test]
    public async Task Get_WhenNoRetros_ShouldReturnEmptyBoard()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        var story = await world.ItemAsync("story", feature);

        var item = await world.GetAsync(feature);

        item.Children.Select(child => child.Item.Id).ShouldBe([story]);
        item.Cards.ShouldBeEmpty();
        item.Actions.ShouldBeEmpty();
        item.Categories.Count.ShouldBe(4);
        item.Categories.ShouldAllBe(column => column.Groups.Count == 0 && column.ToDecide == 0);
        item.Totals.Decidable.ShouldBe(0);
    }

    [Test]
    public async Task Get_WhenTask_ShouldBeNotFound()
    {
        await using var world = await WorldAsync();
        var task = await world.ItemAsync("task");

        using var response = await Api.GetAsync($"/api/projects/{world.Project.Id}/retros/items/{await world.KeyAsync(task)}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
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

        public Task<string> KeyAsync(Guid id) => Connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });

        public async Task PointAsync(Guid itemId, RetroCategory category, string text)
        {
            var retro = await Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.SubAgent, itemId, AgentName: $"agent-{Guid.NewGuid():N}"));
            await Seed.PointAsync(retro, new RetroSeedPoint(category, text));
        }

        public async Task<RetroItemDto> GetAsync(Guid itemId)
        {
            using var response = await Api.GetAsync($"/api/projects/{Project.Id}/retros/items/{await KeyAsync(itemId)}");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            return JsonSerializer.Deserialize<RetroItemDto>(await response.Content.ReadAsStringAsync(), ResponseOptions)!;
        }
    }
}
