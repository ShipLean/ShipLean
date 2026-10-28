using System.Text.Json;

namespace ShipLean.WebApi.Features.LinearApi;

// A stored jsonb document as plain maps, lists and primitives, which is what the JSON scalar writes out.
internal static class LinearJson
{
    public static object? Plain(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Plain(document.RootElement);
    }

    private static object? Plain(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => Plain(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(Plain).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var whole) ? whole : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };
}
