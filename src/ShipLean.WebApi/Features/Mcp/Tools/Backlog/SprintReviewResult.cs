using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Backlog;

// Committed, completed and remaining are the sprint metrics' own numbers. Sections are the same facts as plain text, one
// block per heading, ready to paste into a review or retro doc.
internal sealed record SprintReviewResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("sprint")] string Sprint,
    [property: JsonPropertyName("committed")] decimal Committed,
    [property: JsonPropertyName("completed")] decimal Completed,
    [property: JsonPropertyName("remaining")] decimal Remaining,
    [property: JsonPropertyName("carried_over")] IReadOnlyList<ReviewStory> CarriedOver,
    [property: JsonPropertyName("added_mid_sprint")] IReadOnlyList<ReviewStory> AddedMidSprint,
    [property: JsonPropertyName("velocity")] IReadOnlyList<VelocityPoint> Velocity,
    [property: JsonPropertyName("features")] IReadOnlyList<FeatureLine> Features,
    [property: JsonPropertyName("sections")] IReadOnlyList<ReviewSection> Sections) : McpResult(Project, OpenUrl);
