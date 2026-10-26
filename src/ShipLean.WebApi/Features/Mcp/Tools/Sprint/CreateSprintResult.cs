using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Sprint;

// BasedOn is the sprint whose name and length were copied (null for a project's first sprint).
public sealed record CreateSprintResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("sprint")] string Sprint,
    [property: JsonPropertyName("start_date")] string StartDate,
    [property: JsonPropertyName("end_date")] string EndDate,
    [property: JsonPropertyName("length_days")] int LengthDays,
    [property: JsonPropertyName("goal")] string Goal,
    [property: JsonPropertyName("based_on")] string? BasedOn) : McpResult(Project, OpenUrl);
