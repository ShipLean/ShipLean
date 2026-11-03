using System.Net;
using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Retros.Api;

// The names, status and access of the retro read routes (K6), never the values behind them: the readers are placeholders until their tasks land.
[TestFixture]
public class RetroReadEndpointShapeTests : ApiFixture
{
    private static readonly string Ship = SeedIds.Projects.Ship.ToString();

    private static readonly string[] AggregateProperties = ["actions", "counts", "decidable", "decided", "toDecide"];
    private static readonly string[] CountProperties = ["improve", "notInPlan", "wentBadly", "wentWell"];
    private static readonly string[] FeatureRowProperties = ["item", "lastRetroLabel", "noFeatureRetro", "ownRetros", "retros", "state", "totals"];
    private static readonly string[] ItemRefProperties = ["id", "key", "title", "type"];

    [OneTimeSetUp]
    public async Task InsertSweepRows()
    {
        await using var connection = await OpenDatabaseAsync();
        await RetroSweepData.InsertAsync(connection);
    }

    [Test]
    public async Task Features_ShouldMatchTheContractShape()
    {
        var body = await GetAsync($"/api/projects/{Ship}/retros/features", HttpStatusCode.OK);

        Names(body).ShouldBe(["features", "loose"]);
        foreach (var row in body.GetProperty("features").EnumerateArray().Concat(body.GetProperty("loose").EnumerateArray()))
        {
            Names(row).ShouldBe(FeatureRowProperties);
            Names(row.GetProperty("item")).ShouldBe(ItemRefProperties);
            Names(row.GetProperty("totals")).ShouldBe(AggregateProperties);
            Names(row.GetProperty("totals").GetProperty("counts")).ShouldBe(CountProperties);
        }
    }

    [Test]
    public async Task Item_WhenKeyUnknown_ShouldBeNotFound()
    {
        using var response = await Api.GetAsync($"/api/projects/{Ship}/retros/items/SHIP-99999");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Item_ShouldMatchTheContractShape()
    {
        var body = await GetAsync($"/api/projects/{Ship}/retros/items/SHIP-90", HttpStatusCode.OK);

        Names(body).ShouldBe(["actions", "cards", "categories", "children", "item", "parent", "state", "totals"]);
        Names(body.GetProperty("item")).ShouldBe(ItemRefProperties);
        Names(body.GetProperty("totals")).ShouldBe(AggregateProperties);
        foreach (var column in body.GetProperty("categories").EnumerateArray())
        {
            Names(column).ShouldBe(["category", "groups", "toDecide"]);
        }
    }

    [Test]
    public async Task ActionDialog_ShouldMatchTheContractShape()
    {
        var body = await GetAsync($"/api/projects/{Ship}/retros/action-dialog?point={RetroSweepData.Point}&theme={RetroSweepData.Theme}", HttpStatusCode.OK);

        Names(body).ShouldBe(["offered", "openActions", "selected", "suggestedTheme"]);
    }

    [Test]
    public async Task ActionDetail_ShouldMatchTheContractShape()
    {
        var body = await GetAsync($"/api/retros/actions/{RetroSweepData.Action}", HttpStatusCode.OK);

        Names(body).ShouldBe(["action", "points", "raisedIn", "theme"]);
        Names(body.GetProperty("action")).ShouldBe(["fixType", "key", "pointCount", "points", "sprint", "state", "title", "workItemId"]);
    }

    [Test]
    public async Task ActionDetail_WhenNotAnAction_ShouldBeNotFound()
    {
        using var response = await Api.GetAsync($"/api/retros/actions/{SeedIds.WorkItems.Ship90}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCase("merge")]
    [TestCase("keep-apart")]
    public async Task MergeProposal_WhenUnknown_ShouldBeNotFound(string answer)
    {
        using var response = await Api.PostJsonAsync($"/api/retros/theme-merge-proposals/{Guid.NewGuid()}/{answer}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCase("merge")]
    [TestCase("keep-apart")]
    public async Task MergeProposal_WhenKnown_ShouldBeNoContent(string answer)
    {
        // An answered proposal is a conflict, so each case answers a pending one of its own.
        var proposal = Guid.NewGuid();
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO retro_theme (id, project_id, name, name_set_by, tone) VALUES (@from, @project, @fromName, 'agent', 'bad'), (@into, @project, @intoName, 'agent', 'bad');
            INSERT INTO retro_theme_merge_proposal (id, project_id, from_theme_id, into_theme_id, reason, proposed_by_user_id) VALUES (@proposal, @project, @from, @into, 'Shape', @user);
            """,
            new { proposal, from = Guid.NewGuid(), into = Guid.NewGuid(), fromName = $"From {proposal}", intoName = $"Into {proposal}", project = SeedIds.Projects.Ship, user = SeedIds.Users.Joel });

        using var response = await Api.PostJsonAsync($"/api/retros/theme-merge-proposals/{proposal}/{answer}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<JsonElement> GetAsync(string url, HttpStatusCode expected)
    {
        using var response = await Api.GetAsync(url);

        response.StatusCode.ShouldBe(expected);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static string[] Names(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];
}
