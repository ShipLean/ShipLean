using System.Text.Json.Nodes;
using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Boards.Settings;

// The action-columns migration against boards saved before the Action item existed: its own database, every Action mapping and card setting
// taken out of the seeded boards (what a live board looks like), the script run, then run again.
[TestFixture]
public class ActionColumnsMigrationTests
{
    private const string Script = "202611021000_boards_action_columns.sql";

    [Test]
    public async Task Migration_WhenBoardsHaveNoActionMappings_ShouldMapEveryColumnAndAddTheCardSetting()
    {
        await using var database = await ShipLeanDatabase.CloneSeedAsync();
        await using var connection = await database.OpenAsync();
        var seeded = await SettingsAsync(connection);
        seeded.Count.ShouldBeGreaterThan(8);
        foreach (var (id, settings) in seeded)
        {
            await SaveAsync(connection, id, WithoutAction(settings));
        }

        await connection.ExecuteAsync(ReadScript());

        var migrated = await SettingsAsync(connection);
        foreach (var (id, settings) in migrated)
        {
            settings.ToJsonString().ShouldBe(seeded[id].ToJsonString(), $"board {id}");
        }
        var shipStories = migrated[SeedIds.Boards.ShipStories];
        shipStories["columns"]!.AsArray().ShouldAllBe(c => c!["stateMappings"]!.AsArray().Any(m => m!["type"]!.GetValue<string>() == "action"));
        shipStories["cardFields"]!.AsArray().Select(f => f!["type"]!.GetValue<string>()).ShouldBe(["story", "bug", "action"]);
    }

    [Test]
    public async Task Migration_WhenRunAgain_ShouldChangeNothing()
    {
        await using var database = await ShipLeanDatabase.CloneSeedAsync();
        await using var connection = await database.OpenAsync();
        foreach (var (id, settings) in await SettingsAsync(connection))
        {
            await SaveAsync(connection, id, WithoutAction(settings));
        }
        await connection.ExecuteAsync(ReadScript());
        var once = await SettingsAsync(connection);

        await connection.ExecuteAsync(ReadScript());

        var twice = await SettingsAsync(connection);
        twice.Select(p => (p.Key, p.Value.ToJsonString())).ShouldBe(once.Select(p => (p.Key, p.Value.ToJsonString())), ignoreOrder: true);
    }

    [Test]
    public async Task Migration_WhenColumnsAreCustom_ShouldMapActionStatesByCategoryAndRepeatForGaps()
    {
        await using var database = await ShipLeanDatabase.CloneSeedAsync();
        await using var connection = await database.OpenAsync();
        var states = (await connection.QueryAsync<(Guid Id, string Type, string Name)>(
            "SELECT id, type, name FROM state WHERE project_id = @p AND type IN ('story', 'bug', 'action')", new { p = SeedIds.Projects.Ship })).ToList();
        Guid Of(string type, string name) => states.Single(s => s.Type == type && s.Name == name).Id;
        string Column(string id, string story, string bug) => $$"""{"id":"{{id}}","name":"{{id}}","wipLimit":null,"split":false,"definitionOfDone":"","stateMappings":[{"type":"story","stateId":"{{Of("story", story)}}"},{"type":"bug","stateId":"{{Of("bug", bug)}}"}]}""";
        var board = JsonNode.Parse(
            $$"""{"columns":[{{Column("a", "New", "New")}},{{Column("b", "Active", "Active")}},{{Column("c", "Active", "Active")}},{{Column("d", "Closed", "Closed")}}],"swimlanes":[],"cardFields":[],"styleRules":[],"tagColours":[],"annotations":{},"cardReordering":null,"transitions":[]}""")!;
        await SaveAsync(connection, SeedIds.Boards.ShipStories, board);

        await connection.ExecuteAsync(ReadScript());

        var columns = (await SettingsAsync(connection))[SeedIds.Boards.ShipStories]["columns"]!.AsArray();
        columns.Select(c => c!["stateMappings"]!.AsArray().Single(m => m!["type"]!.GetValue<string>() == "action")!["stateId"]!.GetValue<Guid>())
            .ShouldBe([SeedIds.States.Ship.ActionNew, SeedIds.States.Ship.ActionActive, SeedIds.States.Ship.ActionActive, SeedIds.States.Ship.ActionClosed]);
    }

    private static async Task<Dictionary<Guid, JsonNode>> SettingsAsync(NpgsqlConnection connection)
        => (await connection.QueryAsync<(Guid Id, string Settings)>("SELECT id, settings::text FROM board WHERE level IN ('stories', 'taskboard')"))
            .ToDictionary(r => r.Id, r => JsonNode.Parse(r.Settings)!);

    private static Task SaveAsync(NpgsqlConnection connection, Guid id, JsonNode settings)
        => connection.ExecuteAsync("UPDATE board SET settings = @settings::jsonb WHERE id = @id", new { id, settings = settings.ToJsonString() });

    // The board as it was stored before the type existed.
    private static JsonNode WithoutAction(JsonNode settings)
    {
        var copy = JsonNode.Parse(settings.ToJsonString())!;
        foreach (var column in copy["columns"]!.AsArray())
        {
            var mappings = column!["stateMappings"]!.AsArray();
            foreach (var action in mappings.Where(m => m!["type"]!.GetValue<string>() == "action").ToList())
            {
                mappings.Remove(action);
            }
        }
        var fields = copy["cardFields"]!.AsArray();
        foreach (var action in fields.Where(f => f!["type"]!.GetValue<string>() == "action").ToList())
        {
            fields.Remove(action);
        }
        return copy;
    }

    private static string ReadScript()
    {
        var assembly = typeof(DatabaseScripts).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(Script, StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        return reader.ReadToEnd();
    }
}
