using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.InspectionMetrics;

namespace ShipLean.WebApi.Tests.InspectionMetrics;

// The view's cost (SHIP-1398 CK2, CK3) on a host with Inspection (agent runs) on for the instance (Tempo/Loki store, never contacted)
// and a FakeTaskTokenTotals in place of the store's. Project SHIP starts off; the second test turns it on through the settings route.
// The numbers fill in with CT3, so these tests pin the flag, the shape and what the view asks the store.
[TestFixture]
public class InspectionMetricsCostApiTests : ApiFixture
{
    private static readonly string[] CostKeys = ["available", "note", "periods", "quarters", "range", "headline", "mostTokens", "byFeature"];

    private static readonly string[] HeadlineKeys = ["finished", "soFar", "quarterToDate", "lastQuarter"];

    private readonly FakeTaskTokenTotals tokens = new();

    private static string Url => $"api/projects/{SeedIds.Projects.Ship}/inspection-metrics";

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_INSPECTION"] = "on",
        ["SHIPLEAN_INSPECTION_STORE"] = "tempo-loki",
        ["SHIPLEAN_INSPECTION_TEMPO_URL"] = "http://127.0.0.1:9/tempo",
        ["SHIPLEAN_INSPECTION_LOKI_URL"] = "http://127.0.0.1:9/loki",
    };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<ITaskTokenTotals>();
        services.AddSingleton<ITaskTokenTotals>(tokens);
    };

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

    // With the project on, the view carries a cost in its final shape, and the store was asked for project SHIP's measured tasks from
    // the earliest start until now. Mutations: leave Cost null; drop MostTokens from InspectionCostDto; ask with the project's id.
    [Test]
    [Order(2)]
    public async Task Get_ShouldIncludeCostInFinalShape()
    {
        using var put = await Api.PutJsonAsync("api/inspection/projects/SHIP/settings", new InspectionProjectSettingsDto(true, false, null, null, "tempoLoki"));
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
        query.ProjectKey.ShouldBe("SHIP");
        query.ItemKeys.ShouldNotBeEmpty();
        query.ItemKeys.ShouldAllBe(key => key.StartsWith("SHIP-", StringComparison.Ordinal));
        query.From.ShouldBeLessThan(query.To);
    }
}
