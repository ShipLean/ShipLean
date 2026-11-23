using System.Globalization;
using System.Text.Json;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Prompts;

namespace ShipLean.WebApi.Features.Mcp.Tools.Prompts;

// The arguments the prompt tools share: `prompt` (a camelCase prompt key) and `version` / `based_on` (a version number or built-in).
internal static class PromptArgs
{
    public const string BuiltIn = "built-in";

    public static string ValidKeys { get; } = string.Join(", ", Enum.GetValues<PromptKey>().Select(PromptKeys.ToWire));

    public static PromptKey Key(string? prompt)
        => PromptKeys.TryParse(prompt?.Trim(), out var key)
            ? key
            : throw ApiException.Validation($"There is no prompt \"{prompt}\". Use one of: {ValidKeys}.", "prompt");

    // A version number (1 or more), or null for built-in. Accepts a JSON number, the string built-in, or a string holding a number.
    public static int? Version(JsonElement? value, string field)
    {
        var invalid = ApiException.Validation($"{field} must be a version number such as 3, or {BuiltIn}.", field);
        switch (value)
        {
            case { ValueKind: JsonValueKind.Number } number:
                return number.TryGetInt32(out var whole) && whole > 0 ? whole : throw invalid;
            case { ValueKind: JsonValueKind.String } str:
                var text = str.GetString()?.Trim();
                if (string.Equals(text, BuiltIn, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : throw invalid;
            default:
                throw invalid;
        }
    }

    // "v3", or built-in for null.
    public static string Label(int? version) => version is { } number ? $"v{number}" : BuiltIn;

    // A status, stage or warning kind as camelCase on the wire.
    public static string Wire(Enum value) => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
