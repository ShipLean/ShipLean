using System.Net;
using System.Text.Json;
using Dapper;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// POST backlog/duplicates uses review_backlog's likeness test against the project's live stories and bugs.
[TestFixture]
public class BacklogDuplicatesApiTests : ApiFixture
{
    private static readonly string Url = $"api/projects/{SeedIds.Projects.Ship}/backlog";

    [Test]
    public async Task Duplicates_WhenTitleLooksAlike_ShouldReturnTheMatch()
    {
        var title = await TitleOfAsync("SHIP-141");

        var matches = await CheckAsync("Entirely unrelated zebra", title);

        var match = matches.Single();
        match.GetProperty("index").GetInt32().ShouldBe(1);
        match.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()).ShouldContain("SHIP-141");
        match.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("key").GetString() == "SHIP-141").GetProperty("title").GetString().ShouldBe(title);
    }

    [Test]
    public async Task Duplicates_WhenOnlyOneTitleLooksAlike_ShouldAnswerOnlyThatIndex()
    {
        var title = await TitleOfAsync("SHIP-141");

        var matches = await CheckAsync("Quokka telemetry exporter sandwich", title, "Another unrelated wombat title");

        matches.Select(m => m.GetProperty("index").GetInt32()).ShouldBe([1]);
    }

    [Test]
    public async Task Duplicates_WhenNoneLookAlike_ShouldReturnEmpty()
    {
        var matches = await CheckAsync("Quokka telemetry exporter sandwich");

        matches.ShouldBeEmpty();
    }

    [Test]
    public async Task Duplicates_WhenActionItemMatches_ShouldNotReturnIt()
    {
        const string title = "Axolotl ledger harmonizer pancake";
        await AddAsync(title, "action");

        (await CheckAsync(title)).ShouldBeEmpty();

        await AddAsync(title);
        (await CheckAsync(title)).Single().GetProperty("items").GetArrayLength().ShouldBe(1);
    }

    [Test]
    public async Task Duplicates_WhenRemovedItemMatches_ShouldIgnoreIt()
    {
        const string title = "Quokka telemetry exporter sandwich";
        var created = await AddAsync(title);
        (await CheckAsync(title)).Count.ShouldBe(1);

        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "UPDATE work_item SET state_id = (SELECT id FROM state WHERE project_id = work_item.project_id AND type = 'story' AND category = 'removed' AND archived_at IS NULL LIMIT 1) WHERE id = @created",
                new { created });
        }

        (await CheckAsync(title)).ShouldBeEmpty();
    }

    private async Task<Guid> AddAsync(string title, string type = "story")
    {
        var item = new { type, title, description = (string?)null, points = (int?)null, parentId = (Guid?)null, tasks = Array.Empty<object>() };
        using var response = await Api.PostJsonAsync($"{Url}/items", new { items = new[] { item }, sprintId = (Guid?)null, placement = (object?)null });
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement.GetProperty("created")[0].GetProperty("id").GetGuid();
    }

    private async Task<string> TitleOfAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT title FROM work_item WHERE key = @key", new { key });
    }

    private async Task<List<JsonElement>> CheckAsync(params string[] titles)
    {
        using var response = await Api.PostJsonAsync($"{Url}/duplicates", new { titles });
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return [.. JsonDocument.Parse(text).RootElement.GetProperty("matches").EnumerateArray()];
    }
}
