using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Contracts.Inspection;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.LocalEnvironment.Inspection;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Inspection;
using ShipLean.WebApi.Tests.Inspection.Contract;

namespace ShipLean.WebApi.Tests.InspectionMetrics;

// Cost end to end on the real store (SHIP-1398 CTE): the shared ClickHouse test stack, SHIP-1463's ClickHouse ITaskTokenTotals,
// and GET inspection-metrics. The stack is reused across runs, so the scenario uses a project with a fresh random key: the
// claude-code fixture is sent under that key with its items renamed SHIP-n to <key>-n, so its run is Tagged to <key>-1 and
// linked FromToolCall to <key>-2. Both tasks are created in the new project, started three hours before the fixed clock and
// committed two hours before it, in a running sprint that started six days ago. The fixture is sent with the real time (the
// store stamps seen_at with it), an hour before the clock, so the token window (the earliest start − 1 day to now) holds it.
[TestFixture]
[Category("SHIP-1398")]
public class CostEndToEndTests : ApiFixture
{
    private const string FixtureName = "claude-code-synthetic";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(StoreTimeouts.WaitSeconds);

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    // An hour after the fixture is sent with the real time.
    private readonly FakeTimeProvider clock = new(TimeProvider.System.GetUtcNow().AddHours(1));
    private readonly string key = FreshKey();
    private InspectionStack stack = null!;
    private RawRun expected = null!;
    private string? variantPath;
    private Guid projectId;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_INSPECTION"] = "on",
        ["SHIPLEAN_INSPECTION_STORE"] = "clickhouse",
        ["SHIPLEAN_INSPECTION_CLICKHOUSE_CONNECTION"] = stack.Options("clickhouse").ClickHouseConnection,
    };

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<TimeProvider>(clock);

    // The run is in the store, and the ShipLean schema migrated, before the host's own migrator starts.
    protected override async Task BeforeHostStartAsync()
    {
        stack = await InspectionStack.GetAsync();
        var fixture = InspectionFixtures.Get(FixtureName);
        variantPath = Path.Combine(Path.GetTempPath(), $"shiplean-cost-{Guid.NewGuid():N}.otlp.json");
        await File.WriteAllTextAsync(variantPath, RenameItems(await File.ReadAllTextAsync(fixture.OtlpPath)));
        var load = await OtlpFixtureLoader.SendAsync(stack.CollectorUrl, variantPath, key, TimeProvider.System.GetUtcNow(), 1);
        expected = RawRunJson.ParseRun(RenameItems(load.Rewrite(JsonSerializer.Serialize(fixture.Expected, RawRunJson.Options))));
        await stack.WaitUntilVisibleAsync(load, fixture.Expected.Summary.SpanCount, fixture.Expected.Summary.LogCount, Timeout);
        await ClickHouseSchemaReady.EnsureAsync(stack, Timeout);
    }

    protected override Task AfterHostStopAsync()
    {
        if (variantPath is not null)
        {
            File.Delete(variantPath);
        }
        return Task.CompletedTask;
    }

    [OneTimeSetUp]
    public async Task ArrangeProject()
    {
        using var created = await Api.PostJsonAsync("api/projects", new ProjectCreate($"Cost {key}", key, SeedIds.Projects.Empt, [], [1, 2, 3, 4, 5], null));
        created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        projectId = JsonSerializer.Deserialize<ProjectDto>(await created.Content.ReadAsStringAsync(), Wire)!.Id;
        using var put = await Api.PutJsonAsync($"api/inspection/projects/{key}/settings", new InspectionProjectSettingsDto(true, false, null, null, "clickhouse"));
        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());

        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (gen_random_uuid(), @projectId, 'Sprint 1', CAST(@start AS date), CAST(@end AS date))",
            new { projectId, start = today.AddDays(-6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), end = today.AddDays(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        var active = await StateAsync(connection, SeedIds.States.Empt.TaskActive);
        foreach (var number in new[] { 1, 2 })
        {
            var start = now.AddHours(-3);
            var id = await new WorkItemBuilder(projectId).WithType("task").WithTitle($"Cost task {number}").WithCreatedAt(start.AddHours(-1))
                .WithCreatedBy(SeedIds.Users.Joel).InsertAsync(connection);
            await connection.ExecuteAsync("UPDATE revision SET changed_at = @at WHERE work_item_id = @id AND rev = 1", new { id, at = start.AddHours(-1) });
            await new RevisionBuilder(id).WithRev(2).WithChangedAt(start).WithChangedBy(SeedIds.Users.Joel)
                .WithChanges($$"""[{"field":"state","from":null,"to":"{{active}}"}]""").InsertAsync(connection);
            await connection.ExecuteAsync("UPDATE work_item SET state_id = @active, revision = 2 WHERE id = @id", new { id, active });
            await connection.ExecuteAsync(
                "INSERT INTO work_item_link (id, source_id, link_type, title, url, committed_at) VALUES (gen_random_uuid(), @id, 'url', @title, @url, @at)",
                new { id, title = $"commit c0{number}a1b2", url = $"https://git.example.test/acme/cost/commit/c0{number}a1b2e1e1e1", at = start.AddHours(1) });
            (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id })).ShouldBe($"{key}-{number}");
        }
    }

    // Mutation: read input tokens only in ClickHouseTaskTokenTotals (tokens_out as 0): the tagged task reads 7000, not 8470.
    [Test]
    public async Task Get_WhenTaskHasRecordedRuns_ShouldShowItsTokens()
    {
        var tagged = expected.Summary.Links.Single(link => link.Source == LinkSource.Tagged).ItemKey;
        var toolCall = expected.Summary.Links.Single(link => link.Source == LinkSource.FromToolCall).ItemKey;
        (tagged, toolCall).ShouldBe(($"{key}-1", $"{key}-2"));
        var tokens = expected.Summary.TokensIn.ShouldNotBeNull() + expected.Summary.TokensOut.ShouldNotBeNull();

        using var response = await Api.GetAsync($"api/projects/{projectId}/inspection-metrics");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var cost = JsonSerializer.Deserialize<InspectionMetricsViewDto>(text, Wire)!.Cost.ShouldNotBeNull();
        cost.Available.ShouldBeTrue(cost.Note);
        var row = cost.MostTokens.ShouldHaveSingleItem();
        (row.Key, row.Runs, row.Tokens).ShouldBe((tagged, 1, tokens));
        row.Models.ShouldBe(expected.Summary.Models, ignoreOrder: true);
        var range = cost.Range.ShouldNotBeNull();
        (range.Tasks, range.TasksWithRuns, range.TokensTotal, range.TokensPerTask, range.Kept).ShouldBe((2, 1, tokens, tokens, true));
    }

    // The fixture's item keys SHIP-n, wherever they appear (tags, prompts, escaped tool input), as this project's.
    private string RenameItems(string text) => Regex.Replace(text, @"\bSHIP-(\d+)", match => $"{key}-{match.Groups[1].Value}");

    // The new project's copy of an EMPT state (same type and name).
    private async Task<Guid> StateAsync(Npgsql.NpgsqlConnection connection, Guid emptState)
    {
        return await connection.ExecuteScalarAsync<Guid>(
            "SELECT copy.id FROM state copy JOIN state source ON source.id = @emptState WHERE copy.project_id = @projectId AND copy.type = source.type AND copy.name = source.name",
            new { emptState, projectId });
    }

    // Four random capital letters (the project key rule); the stack is shared across runs, so a fixed key would add up tokens.
    private static string FreshKey() => string.Concat(Enumerable.Range(0, 4).Select(_ => (char)('A' + RandomNumberGenerator.GetInt32(26))));
}
