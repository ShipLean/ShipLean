using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Boards.Settings;

// The merge of a board settings put, section by section. A section is a top-level property of BoardSettingsJson.
internal static class BoardSections
{
    public static (BoardSettingsJson Merged, List<string> MergedSections) Merge(BoardSettingsJson current, BoardSettingsJson basedOn, BoardSettingsJson mine)
    {
        BoardSettingsValidation.CheckWellFormed(basedOn, "basedOn");
        BoardSettingsValidation.CheckWellFormed(mine, "settings");
        // A missing transitions section (a client from before it existed) is no change: the stored one is kept.
        basedOn = basedOn with { Transitions = basedOn.Transitions ?? [] };
        mine = mine with { Transitions = mine.Transitions ?? basedOn.Transitions };
        var currentNode = ToNode(current);
        var baseNode = ToNode(basedOn);
        var mineNode = ToNode(mine);
        var merged = new JsonObject();
        var mergedSections = new List<string>();
        foreach (var (name, currentValue) in currentNode)
        {
            var mineChanged = !JsonNode.DeepEquals(mineNode[name], baseNode[name]);
            var otherChanged = !JsonNode.DeepEquals(currentValue, baseNode[name]);
            merged[name] = (mineChanged ? mineNode[name] : currentValue)?.DeepClone();
            if (!mineChanged && otherChanged)
            {
                mergedSections.Add(name);
            }
        }
        try
        {
            var document = merged.Deserialize<BoardSettingsJson>(PersistedJson.Options)
                ?? throw ApiException.Validation("The board settings are not a document.");
            return (document, mergedSections);
        }
        catch (JsonException)
        {
            throw ApiException.Validation("The board settings are not a document.");
        }
    }

    private static JsonObject ToNode(BoardSettingsJson settings)
        => JsonSerializer.SerializeToNode(settings, PersistedJson.Options)!.AsObject();
}
