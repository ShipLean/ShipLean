using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Groups;

// GET /api/search with the real groups over arranged search_doc rows. Joel is a member of SHIP and not of PART. The item legs find
// nothing here (no item rows), so only the projects and sprints arrays carry results.
[TestFixture]
public class SearchGroupsApiTests : ApiFixture
{
    [Test]
    public async Task Search_WhenProjectNameMatches_ShouldReturnProject()
    {
        await using var connection = await OpenDatabaseAsync();
        await ArrangeShipProjectRowAsync(connection);

        var root = await SearchAsync("zorblax");

        root.GetProperty("projects").EnumerateArray().Select(p => p.GetProperty("key").GetString()).ShouldBe(["SHIP"]);
        root.GetProperty("projects")[0].GetProperty("name").GetString().ShouldBe("Storefront");
    }

    [Test]
    public async Task Search_WhenKeyOnly_ShouldReturnProject()
    {
        await using var connection = await OpenDatabaseAsync();
        await ArrangeShipProjectRowAsync(connection);

        var root = await SearchAsync("SHIP", prefix: true);

        root.GetProperty("projects").EnumerateArray().Select(p => p.GetProperty("key").GetString()).ShouldBe(["SHIP"]);
    }

    [Test]
    public async Task Search_WhenNotMember_ShouldNotReturnTheirProjects()
    {
        await using var connection = await OpenDatabaseAsync();
        await new SearchDocBuilder(SeedIds.Projects.Part, SearchSourceKind.Project, SeedIds.Projects.Part, SearchField.Project, "PART Quuxify\nhidden").InsertAsync(connection);

        var root = await SearchAsync("quuxify");

        root.GetProperty("projects").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task Search_WhenGoalMatches_ShouldReturnSprintWithNameAndGoal()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = await ArrangeSprintAsync(connection, SeedIds.Projects.Ship, "Sprint Alpha", "Harden the frobnicator pipeline");
        await ArrangeRowsAsync(connection, SeedIds.Projects.Ship, sprint, "Sprint Alpha", "Harden the frobnicator pipeline");

        var root = await SearchAsync("frobnicator");

        var sprints = root.GetProperty("sprints");
        sprints.GetArrayLength().ShouldBe(1);
        sprints[0].GetProperty("name").GetString().ShouldBe("Sprint Alpha");
        sprints[0].GetProperty("goal").GetString().ShouldBe("Harden the frobnicator pipeline");
        sprints[0].GetProperty("projectKey").GetString().ShouldBe("SHIP");
    }

    [Test]
    public async Task Search_WhenGoalParaphrased_ShouldFindSprintBySemantics()
    {
        await using var connection = await OpenDatabaseAsync();
        var embedder = Host.Services.GetRequiredService<IEmbedder>();
        var related = await ArrangeSprintAsync(connection, SeedIds.Projects.Ship, "Sprint Beta", "Authentication errors");
        var unrelated = await ArrangeSprintAsync(connection, SeedIds.Projects.Ship, "Sprint Gamma", "Speed up checkout lag");
        await ArrangeRowsAsync(connection, SeedIds.Projects.Ship, related, "Sprint Beta", "Authentication errors", embedder);
        await ArrangeRowsAsync(connection, SeedIds.Projects.Ship, unrelated, "Sprint Gamma", "Speed up checkout lag", embedder);

        var root = await SearchAsync("login broken");

        root.GetProperty("sprints").EnumerateArray().Select(s => s.GetProperty("name").GetString()).ShouldBe(["Sprint Beta"]);
    }

    [Test]
    public async Task Search_WhenGoalEmbeddedByAnotherModel_ShouldNotMatchBySemantics()
    {
        await using var connection = await OpenDatabaseAsync();
        var embedder = Host.Services.GetRequiredService<IEmbedder>();
        var sprint = await ArrangeSprintAsync(connection, SeedIds.Projects.Ship, "Sprint Old", "Authentication errors");
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Sprint, sprint, SearchField.SprintGoal, "Authentication errors")
            .WithEmbedding(await embedder.EmbedQueryAsync("Authentication errors"), new EmbeddingModelId("other-model", "9"))
            .InsertAsync(connection);

        var root = await SearchAsync("login broken");

        root.GetProperty("sprints").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task Search_WhenSprintNameMatches_ShouldReturnSprint()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = await ArrangeSprintAsync(connection, SeedIds.Projects.Ship, "Wibblewobble Run", string.Empty);
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Sprint, sprint, SearchField.SprintName, "Wibblewobble Run").InsertAsync(connection);

        var root = await SearchAsync("wibblewobble");

        root.GetProperty("sprints").EnumerateArray().Select(s => s.GetProperty("name").GetString()).ShouldBe(["Wibblewobble Run"]);
    }

    [Test]
    public async Task Search_WhenSprintNamePrefixed_ShouldReturnSprint()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = await ArrangeSprintAsync(connection, SeedIds.Projects.Ship, "Plinkoid Run", string.Empty);
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Sprint, sprint, SearchField.SprintName, "Plinkoid Run").InsertAsync(connection);

        var root = await SearchAsync("plink", prefix: true);

        root.GetProperty("sprints").EnumerateArray().Select(s => s.GetProperty("name").GetString()).ShouldBe(["Plinkoid Run"]);
    }

    [Test]
    public async Task Search_WhenSprintArchived_ShouldLeaveItOut()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = await ArrangeSprintAsync(connection, SeedIds.Projects.Ship, "Snorkel Sprint", "Snorkel goal");
        await ArrangeRowsAsync(connection, SeedIds.Projects.Ship, sprint, "Snorkel Sprint", "Snorkel goal", Host.Services.GetRequiredService<IEmbedder>());
        await connection.ExecuteAsync("UPDATE sprint SET archived_at = now() WHERE id = @sprint", new { sprint });

        var root = await SearchAsync("snorkel");

        root.GetProperty("sprints").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task Search_WhenNotMember_ShouldNotReturnTheirSprints()
    {
        await using var connection = await OpenDatabaseAsync();
        var sprint = await ArrangeSprintAsync(connection, SeedIds.Projects.Part, "Hidden Sprint", "Hidden xylophone goal");
        await ArrangeRowsAsync(connection, SeedIds.Projects.Part, sprint, "Hidden Sprint", "Hidden xylophone goal", Host.Services.GetRequiredService<IEmbedder>());

        var root = await SearchAsync("xylophone");

        root.GetProperty("sprints").GetArrayLength().ShouldBe(0);
    }

    // The database is shared across tests, so replace the SHIP project row rather than insert a duplicate.
    private static async Task ArrangeShipProjectRowAsync(NpgsqlConnection connection)
    {
        await connection.ExecuteAsync("DELETE FROM search_doc WHERE source_kind = 'project' AND source_id = @id", new { id = SeedIds.Projects.Ship });
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Project, SeedIds.Projects.Ship, SearchField.Project, "SHIP Zorblax Platform\nthe flagship").InsertAsync(connection);
    }

    private async Task<JsonElement> SearchAsync(string text, bool prefix = false)
    {
        using var response = await Api.GetAsync($"api/search?q={Uri.EscapeDataString(text)}{(prefix ? "&prefix=true" : string.Empty)}");
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> ArrangeSprintAsync(NpgsqlConnection connection, Guid projectId, string name, string goal)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date, goal) VALUES (@id, @projectId, @name, '2026-11-02', '2026-11-13', @goal)",
            new { id, projectId, name, goal });
        return id;
    }

    // The rows the indexer writes: a name row, and a goal row that is embedded when an embedder is given.
    private static async Task ArrangeRowsAsync(NpgsqlConnection connection, Guid projectId, Guid sprint, string name, string goal, IEmbedder? embedder = null)
    {
        await new SearchDocBuilder(projectId, SearchSourceKind.Sprint, sprint, SearchField.SprintName, name).InsertAsync(connection);
        var goalRow = new SearchDocBuilder(projectId, SearchSourceKind.Sprint, sprint, SearchField.SprintGoal, goal);
        if (embedder is { ModelId: { } model })
        {
            goalRow.WithEmbedding(await embedder.EmbedQueryAsync(goal), model);
        }

        await goalRow.InsertAsync(connection);
    }
}
