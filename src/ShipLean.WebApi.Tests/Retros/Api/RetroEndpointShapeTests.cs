using System.Net;
using System.Text.Json;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Retros.Api;

// The shape of every retro route, not the values behind it: a member gets 200 with every list present, and a write with a bad body gets 400 validation.
[TestFixture]
public class RetroEndpointShapeTests : ApiFixture
{
    private static readonly string Ship = SeedIds.Projects.Ship.ToString();

    [OneTimeSetUp]
    public async Task InsertPointAndTheme()
    {
        await using var connection = await OpenDatabaseAsync();
        await RetroSweepData.InsertAsync(connection);
    }

    [Test]
    public async Task SprintRetro_WhenMember_ShouldReturnEveryListNonNull()
    {
        var body = await GetOkAsync($"/api/sprints/{SeedIds.Sprints.Sprint22}/retro");

        NonArrays(body, "sprints", "categories", "cards", "coverage", "actionItems", "recurring").ShouldBeEmpty();
        Each(body, "categories", "groups").ShouldBeEmpty();
        Each(body, "cards", "compiledFrom", "points").ShouldBeEmpty();
    }

    [Test]
    public async Task Search_WhenMember_ShouldReturnEveryListNonNull()
    {
        var body = await GetOkAsync($"/api/projects/{Ship}/retros");

        NonArrays(body, "rows", "sprints", "themes").ShouldBeEmpty();
    }

    [Test]
    public async Task Search_WhenFiltered_ShouldStillBeOk()
    {
        var body = await GetOkAsync($"/api/projects/{Ship}/retros?q=ci&category=wentBadly&level=features&role=subAgent&recurring=true");

        NonArrays(body, "rows").ShouldBeEmpty();
    }

    [TestCase("")]
    [TestCase("?range=6")]
    [TestCase("?range=3")]
    [TestCase("?range=all")]
    public async Task Insights_WhenMember_ShouldReturnEveryListNonNull(string query)
    {
        var body = await GetOkAsync($"/api/projects/{Ship}/retros/insights{query}");

        NonArrays(body, "sprints", "themes", "keepsWorking", "actionItems", "impact").ShouldBeEmpty();
        Each(body, "themes", "counts", "actionItems").ShouldBeEmpty();
    }

    [Test]
    public async Task Insights_WhenRangeIsUnknown_ShouldBeValidation()
    {
        using var response = await Api.GetAsync($"/api/projects/{Ship}/retros/insights?range=12");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Themes_WhenMember_ShouldReturnTheList()
    {
        var body = await GetOkAsync($"/api/projects/{Ship}/retros/themes");

        NonArrays(body, "themes").ShouldBeEmpty();
    }

    [Test]
    public async Task WorkItemRetros_WhenMember_ShouldReturnEveryListNonNull()
    {
        var body = await GetOkAsync($"/api/work-items/{SeedIds.WorkItems.Ship90}/retros");

        NonArrays(body, "cards").ShouldBeEmpty();
        Each(body, "cards", "compiledFrom", "points").ShouldBeEmpty();
    }

    [TestCaseSource(nameof(InvalidWrites))]
    public async Task Write_WhenBodyIsInvalid_ShouldBeValidation(string method, string url, string body)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(body);

        using var response = method == "PUT" ? await Api.PutJsonAsync(url, json) : await Api.PostJsonAsync(url, json);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    private async Task<JsonElement> GetOkAsync(string url)
    {
        using var response = await Api.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    // The names among these that are missing or not a JSON array.
    private static IEnumerable<string> NonArrays(JsonElement parent, params string[] names)
        => names.Where(name => !parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array);

    // The same check on every element of the list at `list`, whatever it holds.
    private static IEnumerable<string> Each(JsonElement parent, string list, params string[] names)
        => parent.GetProperty(list).EnumerateArray().SelectMany(element => NonArrays(element, names));

    private static IEnumerable<TestCaseData> InvalidWrites()
    {
        var points = $"/api/projects/{Ship}/retros/points";
        var item = SeedIds.WorkItems.Ship90;
        var sprint = SeedIds.Sprints.Sprint22;
        yield return Case("POST", points, $$"""{"workItemId":"{{item}}","sprintId":"{{sprint}}","category":"improve","text":"x"}""", "AddPoint_WhenItemAndSprintBothNamed");
        yield return Case("POST", points, """{"category":"improve","text":"x"}""", "AddPoint_WhenNoTargetNamed");
        yield return Case("POST", points, $$"""{"workItemId":"{{item}}","category":"improve","text":"  "}""", "AddPoint_WhenTextEmpty");
        yield return Case("POST", points, $$"""{"workItemId":"{{item}}","category":"improve","text":"x","rootCause":"y"}""", "AddPoint_WhenRootCauseOnImprove");
        yield return Case("PUT", $"/api/retros/points/{RetroSweepData.Point}/theme", """{"themeName":"  "}""", "MoveTheme_WhenThemeNameBlank");
        yield return Case("PUT", $"/api/retros/points/{RetroSweepData.Point}/theme", $$"""{"themeId":"{{RetroSweepData.Theme}}","themeName":"Both"}""", "MoveTheme_WhenIdAndNameBothGiven");
        yield return Case("POST", $"/api/retros/points/{RetroSweepData.Point}/action", """{"title":"  ","points":null}""", "CreateAction_WhenTitleBlank");
        yield return Case("PUT", $"/api/retros/themes/{RetroSweepData.Theme}", """{"name":""}""", "Rename_WhenNameEmpty");
        yield return Case("POST", $"/api/projects/{Ship}/retros/themes/merge", $$"""{"sourceIds":["{{RetroSweepData.Theme}}"],"targetId":"{{RetroSweepData.Theme}}"}""", "Merge_WhenSourceIsTarget");
        yield return Case("POST", $"/api/projects/{Ship}/retros/themes/merge", $$"""{"sourceIds":[],"targetId":"{{RetroSweepData.Theme}}"}""", "Merge_WhenNoSources");
    }

    private static TestCaseData Case(string method, string url, string body, string name) => new TestCaseData(method, url, body).SetName(name + "_ShouldBeValidation");
}
