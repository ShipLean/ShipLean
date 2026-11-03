using System.Text.Json;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// A row of the backlog read model, reduced to what the journeys compare.
public sealed record BacklogEntry(string Key, string Title, string Type, string? ParentKey, string? Sprint)
{
    public static BacklogEntry From(JsonElement row) => new(
        row.GetProperty("key").GetString()!,
        row.GetProperty("title").GetString()!,
        row.GetProperty("type").GetProperty("id").GetString()!,
        row.GetProperty("parent") is { ValueKind: JsonValueKind.Object } parent ? parent.GetProperty("key").GetString() : null,
        row.GetProperty("sprint") is { ValueKind: JsonValueKind.Object } sprint ? sprint.GetProperty("name").GetString() : null);
}
