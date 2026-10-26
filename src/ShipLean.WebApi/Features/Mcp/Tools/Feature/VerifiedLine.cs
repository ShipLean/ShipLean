using System.Text.Json.Serialization;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.WebApi.Features.Mcp.Tools.Feature;

// When a Feature's status line was last written or confirmed: the label the app shows ("2 days ago"), and who and what wrote it.
public sealed record VerifiedLine(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("stale")] bool Stale,
    [property: JsonPropertyName("by")] string? By,
    [property: JsonPropertyName("agent")] string? Agent,
    [property: JsonPropertyName("via")] string? Via)
{
    public static VerifiedLine? Of(WorkItemSnapshot snapshot, string? byName, TimeProvider clock)
        => snapshot.StatusVerifiedAt is { } at
            ? new VerifiedLine(DisplayDate.Relative(at, clock), StatusVerifiedDto.IsStale(at, clock.GetUtcNow()), byName, snapshot.StatusVerifiedAgent, snapshot.StatusVerifiedVia)
            : null;
}
