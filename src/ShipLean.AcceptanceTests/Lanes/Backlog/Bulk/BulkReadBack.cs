using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;

// What the server holds for some items, read through the API as the signed-in context.
internal static class BulkReadBack
{
    public sealed record Held(string Key, string? Sprint, string State, decimal? Points, int Revision, string Order);

    public static async Task<Held> ReadAsync(IAPIRequestContext request, string itemKey)
    {
        var item = await BacklogWrites.GetItemAsync(request, itemKey);
        var sprint = item.GetProperty("sprint");
        return new Held(
            itemKey,
            sprint.ValueKind == System.Text.Json.JsonValueKind.Null ? null : sprint.GetProperty("name").GetString(),
            item.GetProperty("state").GetProperty("name").GetString()!,
            item.GetProperty("points").ValueKind == System.Text.Json.JsonValueKind.Null ? null : item.GetProperty("points").GetDecimal(),
            item.GetProperty("revision").GetInt32(),
            item.GetProperty("backlogOrder").GetString()!);
    }

    // The items' keys in backlog order (lowest first), the place the server gives them.
    public static async Task<List<string>> InOrderAsync(IAPIRequestContext request, params string[] itemKeys)
    {
        var held = new List<Held>();
        foreach (var key in itemKeys)
        {
            held.Add(await ReadAsync(request, key));
        }

        return held.OrderBy(h => h.Order, StringComparer.Ordinal).Select(h => h.Key).ToList();
    }
}
