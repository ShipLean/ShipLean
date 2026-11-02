using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using Npgsql;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// What the bulk write tests send and read: stories made through the real endpoint, the bulk call, and the stored rows.
internal static class BulkWriteWire
{
    public static string Url(Guid projectId) => $"api/projects/{projectId}/backlog/bulk";

    public static JsonObject Points(decimal value) => new() { ["points"] = new JsonObject { ["value"] = value } };

    public static JsonObject Sprint(Guid? sprintId) => new() { ["sprint"] = new JsonObject { ["value"] = sprintId } };

    public static JsonObject Slot(string slot, Guid? before = null) => new() { ["slot"] = slot, ["beforeItemId"] = before };

    public static JsonObject Item(Guid id, int revision, JsonObject? changes, JsonObject? placement)
        => new() { ["id"] = id, ["expectedRevision"] = revision, ["changes"] = changes, ["placement"] = placement };

    public static async Task<(Guid Id, int Revision)> CreateAsync(ApiClient api, Guid projectId, string title, string type = "story", Guid? parentId = null)
    {
        using var response = await api.PostJsonAsync("api/work-items", new JsonObject
        {
            ["projectId"] = projectId, ["type"] = type, ["title"] = title, ["parentId"] = parentId, ["sprintId"] = null, ["changes"] = null,
        });
        var json = await ReadAsync(response);
        return (json.GetProperty("id").GetGuid(), json.GetProperty("revision").GetInt32());
    }

    public static async Task<List<(Guid Id, int Revision)>> CreateManyAsync(ApiClient api, Guid projectId, int count, string prefix)
    {
        var made = new List<(Guid, int)>();
        for (var n = 1; n <= count; n++)
        {
            made.Add(await CreateAsync(api, projectId, $"{prefix} {n}"));
        }

        return made;
    }

    public static async Task<HttpResponseMessage> PostAsync(ApiClient api, Guid projectId, IReadOnlyList<JsonObject> items)
        => await api.PostJsonAsync(Url(projectId), new JsonObject { ["items"] = new JsonArray([.. items]) });

    public static async Task<JsonElement> BulkAsync(ApiClient api, Guid projectId, IReadOnlyList<JsonObject> items)
    {
        using var response = await PostAsync(api, projectId, items);
        return await ReadAsync(response);
    }

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, text);
        return JsonDocument.Parse(text).RootElement;
    }

    public static IReadOnlyList<Guid> Ids(JsonElement array, Func<JsonElement, JsonElement>? pick = null)
        => [.. array.EnumerateArray().Select(e => (pick?.Invoke(e) ?? e).GetProperty("id").GetGuid())];

    // The project's rows in the backlog's own order, as the database sorts them (the order the reads use).
    public static async Task<List<Guid>> BacklogOrderAsync(NpgsqlConnection database, Guid projectId)
        => [.. await database.QueryAsync<Guid>("SELECT id FROM work_item WHERE project_id = @projectId ORDER BY backlog_order, id", new { projectId })];

    public static async Task<(int Revision, decimal? Points, Guid? Sprint)> RowAsync(NpgsqlConnection database, Guid id)
        => await database.QuerySingleAsync<(int, decimal?, Guid?)>("SELECT revision, story_points, sprint_id FROM work_item WHERE id = @id", new { id });

    public static async Task<List<(int Rev, bool Bulk)>> RevisionsAsync(NpgsqlConnection database, Guid id)
        => [.. await database.QueryAsync<(int, bool)>("SELECT rev, coalesce(bulk, false) FROM revision WHERE work_item_id = @id ORDER BY rev", new { id })];
}
