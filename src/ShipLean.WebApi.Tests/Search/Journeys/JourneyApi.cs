using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Journeys;

// The writes and reads of the search journeys, all through the real API as the client's signed-in user.
public static class JourneyApi
{
    public sealed record Created(Guid Id, string Key, int Revision);

    public static async Task<Created> CreateItemAsync(ApiClient api, Guid projectId, string title, string type = "story")
    {
        var body = new JsonObject { ["projectId"] = projectId, ["type"] = type, ["title"] = title, ["parentId"] = null, ["sprintId"] = null, ["changes"] = null };
        using var response = await api.PostJsonAsync("api/work-items", body);
        return await ReadWriteResultAsync(response);
    }

    public static async Task<Created> RetitleAsync(ApiClient api, Created item, string title)
    {
        var body = new JsonObject { ["expectedRevision"] = item.Revision, ["changes"] = new JsonObject { ["title"] = new JsonObject { ["value"] = title } } };
        using var response = await api.PatchJsonAsync($"api/work-items/{item.Id}", body);
        return await ReadWriteResultAsync(response);
    }

    public static async Task RemoveAsync(ApiClient api, Created item)
    {
        using var response = await api.PostJsonAsync($"api/work-items/{item.Id}/remove", new JsonObject { ["expectedRevision"] = item.Revision });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    public static async Task CommentAsync(ApiClient api, Guid itemId, string body)
    {
        using var response = await api.PostJsonAsync($"api/work-items/{itemId}/comments", new JsonObject { ["body"] = body });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    public static async Task CreateSprintAsync(ApiClient api, Guid projectId, string name, string goal, int startOffsetDays)
    {
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);
        var body = new JsonObject
        {
            ["name"] = name,
            ["startDate"] = today.AddDays(startOffsetDays).ToString("yyyy-MM-dd"),
            ["endDate"] = today.AddDays(startOffsetDays + 13).ToString("yyyy-MM-dd"),
            ["goal"] = goal,
        };
        using var response = await api.PostJsonAsync($"api/projects/{projectId}/sprints", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    // A full pass: it syncs every source from the real entities and embeds what is pending, before the call returns.
    public static async Task ReindexAsync(ApiClient api)
    {
        using var response = await api.PostJsonAsync("api/admin/search/reindex?full=true");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    public static async Task<JsonElement> SearchAsync(ApiClient api, string query)
    {
        using var response = await api.GetAsync($"api/search?q={Uri.EscapeDataString(query)}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public static IReadOnlyList<string> ItemKeys(JsonElement search)
        => search.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()!).ToList();

    private static async Task<Created> ReadWriteResultAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var root = JsonDocument.Parse(text).RootElement;
        return new Created(root.GetProperty("id").GetGuid(), root.GetProperty("key").GetString()!, root.GetProperty("revision").GetInt32());
    }
}
