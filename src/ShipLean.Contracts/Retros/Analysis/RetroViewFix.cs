using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

// Action is the action item that carries the fix's pattern (one per problem pattern), null when the fix made none.
public sealed record RetroViewFix(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("fix_type")] string FixType,
    [property: JsonPropertyName("enforcement")] RetroEnforcement Enforcement,
    [property: JsonPropertyName("enforced_by")] string? EnforcedBy,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("action")] RetroViewAction? Action);
