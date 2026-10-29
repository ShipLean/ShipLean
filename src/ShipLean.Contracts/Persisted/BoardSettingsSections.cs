using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShipLean.Contracts.Persisted;

// The sections (top-level properties of BoardSettingsJson) where two documents differ.
public static class BoardSettingsSections
{
    public static IReadOnlyList<string> Changed(BoardSettingsJson before, BoardSettingsJson after)
    {
        var beforeNode = ToNode(before);
        var afterNode = ToNode(after);
        return [.. beforeNode.Where(p => !JsonNode.DeepEquals(p.Value, afterNode[p.Key])).Select(p => p.Key)];
    }

    private static JsonObject ToNode(BoardSettingsJson settings)
        => JsonSerializer.SerializeToNode(settings, PersistedJson.Options)!.AsObject();
}
