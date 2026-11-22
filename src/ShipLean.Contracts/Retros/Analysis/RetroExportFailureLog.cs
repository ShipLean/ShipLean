using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

public sealed record RetroExportFailureLog(
    [property: JsonPropertyName("blockers")] IReadOnlyList<string> Blockers,
    [property: JsonPropertyName("assumptions_disproved")] IReadOnlyList<string> AssumptionsDisproved,
    [property: JsonPropertyName("friction")] IReadOnlyList<string> Friction);
