using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItems;

// A Bug's Found before release flag through the real endpoints: it is written, read back, recorded as a revision, and
// refused on any other type.
[TestFixture]
public class FoundBeforeReleaseTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static JsonObject Create(string type, string title)
        => new()
        {
            ["projectId"] = SeedIds.Projects.Ship,
            ["type"] = type,
            ["title"] = title,
            ["parentId"] = null,
            ["sprintId"] = null,
            ["changes"] = null,
        };

    private static JsonObject Patch(int expectedRevision, bool value)
        => new()
        {
            ["expectedRevision"] = expectedRevision,
            ["changes"] = new JsonObject { ["foundBeforeRelease"] = new JsonObject { ["value"] = value } },
        };

    private async Task<WriteResult> CreateItemAsync(string type, string title)
    {
        using var response = await Api.PostJsonAsync("api/work-items", Create(type, title));
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<WriteResult>(text, Wire)!;
    }

    private async Task<WorkItemDto> GetAsync(Guid id)
    {
        using var response = await Api.GetAsync($"api/work-items/{id}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<WorkItemDto>(text, Wire)!;
    }

    [Test]
    public async Task Update_WhenBugMarked_ShouldPersistAndRecordRevision()
    {
        var bug = await CreateItemAsync("bug", "Found by review");
        (await GetAsync(bug.Id)).FoundBeforeRelease.ShouldBeFalse();

        using var response = await Api.PatchJsonAsync($"api/work-items/{bug.Id}", Patch(bug.Revision, true));

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var written = JsonSerializer.Deserialize<WriteResult>(text, Wire)!;
        written.Revision.ShouldBe(bug.Revision + 1);
        written.Changes.Select(c => c.Field).ShouldBe(["foundBeforeRelease"]);
        (await GetAsync(bug.Id)).FoundBeforeRelease.ShouldBeTrue();
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<bool>("SELECT found_before_release FROM work_item WHERE id = @id", new { id = bug.Id })).ShouldBeTrue();
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM revision WHERE work_item_id = @id AND rev = @rev AND changes @> '[{\"field\":\"foundBeforeRelease\"}]'::jsonb",
            new { id = bug.Id, rev = written.Revision })).ShouldBe(1);
    }

    [Test]
    public async Task Update_WhenNotBug_ShouldReturnValidation()
    {
        var story = await CreateItemAsync("story", "Not a bug");

        using var response = await Api.PatchJsonAsync($"api/work-items/{story.Id}", Patch(story.Revision, true));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("foundBeforeRelease");
        (await GetAsync(story.Id)).FoundBeforeRelease.ShouldBeFalse();
    }

    [Test]
    public async Task Update_WhenBugUnmarked_ShouldClearIt()
    {
        var bug = await CreateItemAsync("bug", "Marked then cleared");
        using var marked = await Api.PatchJsonAsync($"api/work-items/{bug.Id}", Patch(bug.Revision, true));
        marked.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var cleared = await Api.PatchJsonAsync($"api/work-items/{bug.Id}", Patch(bug.Revision + 1, false));

        cleared.StatusCode.ShouldBe(HttpStatusCode.OK, await cleared.Content.ReadAsStringAsync());
        (await GetAsync(bug.Id)).FoundBeforeRelease.ShouldBeFalse();
    }
}
