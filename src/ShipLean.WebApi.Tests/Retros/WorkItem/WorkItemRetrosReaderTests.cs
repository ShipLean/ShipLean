using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
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

namespace ShipLean.WebApi.Tests.Retros.WorkItem;

// GET api/work-items/{id}/retros, arranged with RetroSeed and RetroHistory in a project of the test's own.
public sealed class WorkItemRetrosReaderTests : ApiFixture
{
    private static readonly Guid Joel = SeedIds.Users.Joel;

    // The host serialises enums as camelCase strings; JsonSerializerOptions.Web alone doesn't read them back.
    private static readonly JsonSerializerOptions ResponseOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    [Test]
    public async Task Get_WhenStoryHasTwoAgentRetros_ShouldReturnBothNewestFirst()
    {
        await using var world = await WorldAsync();
        var story = await world.ItemAsync("story");
        var older = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, AgentName: "agent-a", CreatedAt: RetroArrangement.Noon(2026, 2, 10)));
        var newer = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, AgentName: "agent-b", CreatedAt: RetroArrangement.Noon(2026, 2, 12)));
        await world.Seed.PointAsync(older, new RetroSeedPoint(RetroCategory.WentWell, "Older point."));
        await world.Seed.PointAsync(newer, new RetroSeedPoint(RetroCategory.WentBadly, "Newer point."));
        await world.Seed.FailureAsync(newer, RetroFailureKind.Friction, "Slow cache.");
        var otherStory = await world.ItemAsync("story");
        await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: otherStory, AgentName: "agent-c"));

        var retros = await GetAsync(story);

        retros.Cards.Select(card => card.Id).ShouldBe([newer, older]);
        retros.Cards[0].Points.Single().Text.ShouldBe("Newer point.");
        retros.Cards[0].FailureLog.Friction.ShouldBe(["Slow cache."]);
    }

    [Test]
    public async Task Rows_WhenTwoRetros_ShouldCountEach()
    {
        await using var world = await WorldAsync();
        var story = await world.ItemAsync("story");
        var older = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, AgentName: "agent-a", CreatedAt: RetroArrangement.Noon(2026, 2, 10)));
        var newer = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, AgentName: "agent-b", CreatedAt: RetroArrangement.Noon(2026, 2, 12)));
        await world.Seed.PointAsync(older, new RetroSeedPoint(RetroCategory.WentBadly, "Slow."));
        await world.Seed.PointAsync(newer, new RetroSeedPoint(RetroCategory.WentWell, "Fast."));
        await world.Seed.PointAsync(newer, new RetroSeedPoint(RetroCategory.WentWell, "Clear."));
        await world.Seed.PointAsync(newer, new RetroSeedPoint(RetroCategory.Improve, "Plan.", NotNeededReason: "Fixed already.", DecidedById: Joel, DecidedAt: RetroArrangement.Noon(2026, 2, 13)));
        await world.Seed.PointAsync(newer, new RetroSeedPoint(RetroCategory.NotInPlan, "Surprise."));

        var retros = await GetAsync(story);

        retros.Rows.Select(row => row.RetroId).ShouldBe([newer, older]);
        var first = retros.Rows[0].Totals;
        first.Counts.ShouldBe(new RetroCategoryCountsDto(2, 1, 0, 1));
        (first.ToDecide, first.Decided, first.Decidable).ShouldBe((1, 1, 2));
        var second = retros.Rows[1].Totals;
        second.Counts.ShouldBe(new RetroCategoryCountsDto(0, 0, 1, 0));
        (second.ToDecide, second.Decided, second.Decidable).ShouldBe((1, 0, 1));
    }

    [Test]
    public async Task Get_WhenFeature_ShouldReturnConductorRetro()
    {
        await using var world = await WorldAsync();
        var feature = await world.ItemAsync("feature");
        var child = await world.ItemAsync("story");
        var childRetro = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: child, AgentName: "agent-a"));
        var conductor = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, RetroRole.Conductor, WorkItemId: feature));
        await world.Seed.SourceAsync(conductor, childRetro);
        await world.Seed.PointAsync(conductor, new RetroSeedPoint(RetroCategory.Improve, "Plan smaller."));

        var retros = await GetAsync(feature);

        var card = retros.Cards.Single();
        card.Id.ShouldBe(conductor);
        card.Author.Role.ShouldBe(RetroRole.Conductor);
        card.WorkItem.ShouldNotBeNull().Id.ShouldBe(feature);
        card.CompiledFrom.Single().Id.ShouldBe(child);
    }

    [Test]
    public async Task Get_WhenNone_ShouldReturnEmpty()
    {
        await using var world = await WorldAsync();
        var story = await world.ItemAsync("story");
        var archived = await world.Seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, AgentName: "agent-a"));
        await world.Connection.ExecuteAsync("UPDATE retrospective SET archived_at = now() WHERE id = @archived", new { archived });

        var retros = await GetAsync(story);

        retros.Cards.ShouldBeEmpty();
    }

    [Test]
    public async Task Get_WhenTask_ShouldReturnEmpty()
    {
        await using var world = await WorldAsync();
        var task = await world.ItemAsync("task");

        var retros = await GetAsync(task);

        retros.Cards.ShouldBeEmpty();
    }

    [Test]
    public async Task Get_WhenNonMember_ShouldBeNotFound()
    {
        await using var world = await WorldAsync();
        var story = await world.ItemAsync("story");

        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"/api/work-items/{story}/retros");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    private async Task<WorkItemRetrosDto> GetAsync(Guid workItemId)
    {
        using var response = await Api.GetAsync($"/api/work-items/{workItemId}/retros");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonSerializer.Deserialize<WorkItemRetrosDto>(await response.Content.ReadAsStringAsync(), ResponseOptions)!;
    }

    private async Task<World> WorldAsync()
    {
        var connection = await OpenDatabaseAsync();
        var project = await ArrangedProject.CreateAsync(connection, (Joel, ProjectRole.Member));
        return new World(connection, project);
    }

    // The arranged project. It owns the connection until the fixture ends the scenario.
    private sealed record World(NpgsqlConnection Connection, ArrangedProject Project) : IAsyncDisposable
    {
        public RetroSeed Seed { get; } = new(Connection, Project.Id);

        public ValueTask DisposeAsync() => Connection.DisposeAsync();

        public async Task<Guid> ItemAsync(string type)
            => await RetroHistory.CreateAsync(Connection, new WorkItemBuilder(Project.Id).WithType(type));
    }
}
