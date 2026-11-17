using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.InspectionMetrics;

namespace ShipLean.WebApi.Tests.InspectionMetrics;

// The view's cost (SHIP-1398 CK2, CK3) on a host with Inspection (agent runs) on for the instance (Tempo/Loki store, never contacted)
// and a FakeTaskTokenTotals in place of the store's. The empty project EMPT gets one sprint (9 to 20 Nov 2026) and one task, Active
// at 09:00 on 10 Nov and Resolved at 18:00; the clock is fixed on 18 Nov. EMPT starts off; the second test turns it on through the
// settings route. The numbers fill in with CT3, so these tests pin the flag, the shape and what the view asks the store.
[TestFixture]
public class InspectionMetricsCostApiTests : ApiFixture
{
    private static readonly string[] CostKeys = ["available", "note", "periods", "quarters", "range", "headline", "mostTokens", "byFeature"];

    private static readonly string[] HeadlineKeys = ["finished", "soFar", "quarterToDate", "lastQuarter"];

    private static readonly Guid Empt = SeedIds.Projects.Empt;
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Started = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTaskTokenTotals tokens = new();
    private readonly FakeTimeProvider clock = new(Now);
    private string taskKey = string.Empty;

    private static string Url => $"api/projects/{Empt}/inspection-metrics";

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_INSPECTION"] = "on",
        ["SHIPLEAN_INSPECTION_STORE"] = "tempo-loki",
        ["SHIPLEAN_INSPECTION_TEMPO_URL"] = "http://127.0.0.1:9/tempo",
        ["SHIPLEAN_INSPECTION_LOKI_URL"] = "http://127.0.0.1:9/loki",
    };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<TimeProvider>(clock);
        services.RemoveAll<ITaskTokenTotals>();
        services.AddSingleton<ITaskTokenTotals>(tokens);
    };

    [OneTimeSetUp]
    public async Task ArrangeTask()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @Empt, 'Sprint 1', DATE '2026-11-09', DATE '2026-11-20')",
            new { id = Guid.NewGuid(), Empt });
        var id = await new WorkItemBuilder(Empt).WithType("task").WithCreatedAt(Started.AddHours(-1)).WithCreatedBy(SeedIds.Users.Joel).InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @at WHERE work_item_id = @id AND rev = 1", new { id, at = Started.AddHours(-1) });
        await MoveAsync(connection, id, 2, Started, SeedIds.States.Empt.TaskActive);
        await MoveAsync(connection, id, 3, Started.AddHours(9), SeedIds.States.Empt.TaskResolved);
        taskKey = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }) ?? throw new InvalidOperationException();
    }

    // The instance is on but the project is not: no cost and no token read. Mutation: skip the IInspectionFlag check.
    [Test]
    [Order(1)]
    public async Task Get_WhenProjectAgentRunsOff_ShouldReturnCostNull()
    {
        using var response = await Api.GetAsync(Url);

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        using var json = JsonDocument.Parse(text);
        json.RootElement.GetProperty("cost").ValueKind.ShouldBe(JsonValueKind.Null);
        tokens.Queries.ShouldBeEmpty();
    }

    // With the project on, the view carries a cost in its final shape, and the store was asked for EMPT's measured task from its start
    // until now. Mutations: leave Cost null; drop MostTokens from InspectionCostDto; ask from the window's first day instead of the
    // earliest start.
    [Test]
    [Order(2)]
    public async Task Get_ShouldIncludeCostInFinalShape()
    {
        using var put = await Api.PutJsonAsync("api/inspection/projects/EMPT/settings", new InspectionProjectSettingsDto(true, false, null, null, "tempoLoki"));
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await Api.GetAsync(Url);

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        using var json = JsonDocument.Parse(text);
        var cost = json.RootElement.GetProperty("cost");
        cost.ValueKind.ShouldBe(JsonValueKind.Object);
        cost.EnumerateObject().Select(p => p.Name).ShouldBe(CostKeys, ignoreOrder: true);
        cost.GetProperty("headline").EnumerateObject().Select(p => p.Name).ShouldBe(HeadlineKeys, ignoreOrder: true);
        foreach (var list in new[] { "periods", "quarters", "mostTokens", "byFeature" })
        {
            cost.GetProperty(list).ValueKind.ShouldBe(JsonValueKind.Array, list);
        }
        var query = tokens.Queries.ShouldHaveSingleItem();
        query.ShouldBe(query with { ProjectKey = "EMPT", From = Started, To = Now });
        query.ItemKeys.ShouldBe([taskKey]);
    }

    private static async Task MoveAsync(Npgsql.NpgsqlConnection connection, Guid id, int rev, DateTimeOffset at, Guid state)
    {
        await new RevisionBuilder(id).WithRev(rev).WithChangedAt(at).WithChangedBy(SeedIds.Users.Joel)
            .WithChanges($$"""[{"field":"state","from":null,"to":"{{state}}"}]""").InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @state, revision = @rev WHERE id = @id", new { id, state, rev });
    }
}
