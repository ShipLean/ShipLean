using System.Net;
using System.Text.Json;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// The backlog write endpoints answer in their final shapes, with real access and limit checks. The writes behind them are
// placeholders, so these assert shape, access and limits only, never a written value.
[TestFixture]
public class BacklogWritesContractTests : ApiFixture
{
    private static readonly string Url = $"api/projects/{SeedIds.Projects.Ship}/backlog";

    private static readonly object Points = new { points = new { value = 5 } };

    [Test]
    public async Task Bulk_WhenSent_ShouldAnswerInFinalShape()
    {
        using var response = await Api.PostJsonAsync($"{Url}/bulk", new { items = new[] { Row(SeedIds.WorkItems.Ship141), Row(SeedIds.WorkItems.Ship144) } });

        var body = await ReadAsync(response);
        body.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ShouldBe(["stale", "written"]);
        body.GetProperty("written").EnumerateArray().Select(w => w.GetProperty("id").GetGuid()).ShouldBe([SeedIds.WorkItems.Ship141, SeedIds.WorkItems.Ship144]);
        body.GetProperty("written")[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ShouldBe(["changes", "id", "key", "notices", "revision"]);
        body.GetProperty("stale").ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [Test]
    public async Task Bulk_WhenOverLimit_ShouldAnswerValidation()
    {
        var items = Enumerable.Range(0, 101).Select(_ => Row(Guid.NewGuid())).ToList();

        using var response = await Api.PostJsonAsync($"{Url}/bulk", new { items });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Bulk_WhenDuplicateIds_ShouldAnswerValidation()
    {
        using var response = await Api.PostJsonAsync($"{Url}/bulk", new { items = new[] { Row(SeedIds.WorkItems.Ship141), Row(SeedIds.WorkItems.Ship141) } });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Bulk_WhenBeforeTargetIsInAnotherBlock_ShouldAccept()
    {
        var first = new { slot = "first", beforeItemId = (Guid?)null };
        var before = new { slot = "before", beforeItemId = (Guid?)SeedIds.WorkItems.Ship141 };
        var items = new[] { Placed(SeedIds.WorkItems.Ship141, first), Placed(SeedIds.WorkItems.Ship144, before) };

        using var response = await Api.PostJsonAsync($"{Url}/bulk", new { items });

        (await ReadAsync(response)).GetProperty("written").GetArrayLength().ShouldBe(2);
    }

    [Test]
    public async Task Bulk_WhenBeforeTargetIsInSameBlock_ShouldAnswerValidation()
    {
        var before = new { slot = "before", beforeItemId = (Guid?)SeedIds.WorkItems.Ship141 };
        var items = new[] { Placed(SeedIds.WorkItems.Ship141, before), Placed(SeedIds.WorkItems.Ship144, before) };

        using var response = await Api.PostJsonAsync($"{Url}/bulk", new { items });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Bulk_WhenNotMember_ShouldAnswerNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Kai).PostJsonAsync($"{Url}/bulk", new { items = new[] { Row(SeedIds.WorkItems.Ship141) } });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Add_WhenSent_ShouldAnswerInFinalShape()
    {
        var item = new { type = "story", title = "Pay with a saved card", description = (string?)null, points = 5, parentId = (Guid?)null, tasks = new[] { new { title = "Show cards", points = 2 } } };

        using var response = await Api.PostJsonAsync($"{Url}/items", new { items = new[] { item }, sprintId = (Guid?)null, placement = (object?)null });

        var body = await ReadAsync(response);
        body.EnumerateObject().Select(p => p.Name).ShouldBe(["created"]);
        body.GetProperty("created").GetArrayLength().ShouldBe(2);
        body.GetProperty("created")[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ShouldBe(["changes", "id", "key", "notices", "revision"]);
    }

    [Test]
    public async Task Add_WhenOverLimit_ShouldAnswerValidation()
    {
        var tasks = Enumerable.Range(0, 100).Select(n => new { title = $"Task {n}", points = (int?)null }).ToList();
        var item = new { type = "story", title = "Big", description = (string?)null, points = (int?)null, parentId = (Guid?)null, tasks };

        using var response = await Api.PostJsonAsync($"{Url}/items", new { items = new[] { item }, sprintId = (Guid?)null, placement = (object?)null });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Duplicates_WhenSent_ShouldAnswerInFinalShape()
    {
        using var response = await Api.PostJsonAsync($"{Url}/duplicates", new { titles = new[] { "Pay with a saved card" } });

        var body = await ReadAsync(response);
        body.EnumerateObject().Select(p => p.Name).ShouldBe(["matches"]);
        body.GetProperty("matches").ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [Test]
    public async Task Duplicates_WhenOverLimit_ShouldAnswerValidation()
    {
        using var response = await Api.PostJsonAsync($"{Url}/duplicates", new { titles = Enumerable.Range(0, 101).Select(n => $"Title {n}") });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    private static object Row(Guid id) => new { id, expectedRevision = 1, changes = Points, placement = (object?)null };

    private static object Placed(Guid id, object placement) => new { id, expectedRevision = 1, changes = (object?)null, placement };

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement;
    }
}
