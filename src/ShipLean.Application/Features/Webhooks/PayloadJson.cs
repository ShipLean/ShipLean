using System.Globalization;
using System.Text.Json;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Webhooks;

// Reading the few fields the parsers need from a provider's JSON. A field that is missing or of the wrong kind is a
// validation error: the payload isn't the event it claims to be.
internal static class PayloadJson
{
    private static readonly string[] TimestampFormats = ["yyyy-MM-dd HH:mm:ss 'UTC'", "yyyy-MM-dd HH:mm:ss zzz"];

    public static JsonElement Object(JsonElement parent, string name)
        => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object
            ? value
            : throw Missing(name);

    public static string Text(JsonElement parent, string name)
        => OptionalText(parent, name) ?? throw Missing(name);

    public static string? OptionalText(JsonElement parent, string name)
        => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static bool Flag(JsonElement parent, string name)
        => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    // ISO 8601 (GitHub, newer GitLab) or GitLab's "2026-10-25 09:30:00 UTC" / "2026-10-25 09:30:00 +0700", as UTC.
    public static DateTimeOffset Timestamp(JsonElement parent, string name)
    {
        var text = Text(parent, name);
        if (DateTimeOffset.TryParseExact(text, TimestampFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var exact)
            || DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out exact))
        {
            return exact.ToUniversalTime();
        }
        throw ApiException.Validation($"The payload's {name} is not a timestamp.");
    }

    private static ApiException Missing(string name) => ApiException.Validation($"The payload has no {name}.");
}
