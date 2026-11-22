using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Retros.Analysis;

public sealed record RetroExportCommit(
    [property: JsonPropertyName("repository")] string Repository,
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("paths")] IReadOnlyList<string> Paths);
