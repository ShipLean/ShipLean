using System.Text.Json;

namespace ShipLean.Contracts.Prompts;

// The camelCase wire value of a PromptKey (C1) and the way back: only the 12 names, never a number or another case.
public static class PromptKeys
{
    private static readonly Dictionary<string, PromptKey> ByWire = Enum.GetValues<PromptKey>().ToDictionary(ToWire, StringComparer.Ordinal);

    public static string ToWire(PromptVersionSource source) => JsonNamingPolicy.CamelCase.ConvertName(source.ToString());

    public static string ToWire(PromptKey key) => JsonNamingPolicy.CamelCase.ConvertName(key.ToString());

    public static bool TryParse(string? wire, out PromptKey key)
    {
        key = default;
        return wire is not null && ByWire.TryGetValue(wire, out key);
    }
}
