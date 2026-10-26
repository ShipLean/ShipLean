using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Status;

// Dates are yyyy-MM-dd. DaysLeft is set only while the sprint is running. Capacity is the team's entered total (null when
// none is entered). Trend compares the last burndown point with the ideal line: ahead, on_track or behind.
internal sealed record SprintStatusResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("sprint")] string Sprint,
    [property: JsonPropertyName("goal")] string Goal,
    [property: JsonPropertyName("start_date")] string StartDate,
    [property: JsonPropertyName("end_date")] string EndDate,
    [property: JsonPropertyName("days_left")] int? DaysLeft,
    [property: JsonPropertyName("time_label")] string TimeLabel,
    [property: JsonPropertyName("committed")] decimal Committed,
    [property: JsonPropertyName("completed")] decimal Completed,
    [property: JsonPropertyName("remaining")] decimal Remaining,
    [property: JsonPropertyName("capacity")] decimal? Capacity,
    [property: JsonPropertyName("trend")] string? Trend,
    [property: JsonPropertyName("burndown")] IReadOnlyList<SprintStatusResult.BurndownEntry> Burndown,
    [property: JsonPropertyName("states")] IReadOnlyList<SprintStatusResult.StateGroup> States,
    [property: JsonPropertyName("blocked")] IReadOnlyList<SprintStatusResult.StatusItem> Blocked,
    [property: JsonPropertyName("unparented_tasks")] IReadOnlyList<SprintStatusResult.StatusItem> UnparentedTasks) : McpResult(Project, OpenUrl)
{
    internal sealed record BurndownEntry(
        [property: JsonPropertyName("date")] string Date,
        [property: JsonPropertyName("remaining")] decimal Remaining,
        [property: JsonPropertyName("ideal")] decimal Ideal);

    internal sealed record StateGroup(
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("items")] IReadOnlyList<StatusItem> Items);

    internal sealed record StatusItem(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("assignee")] string? Assignee,
        [property: JsonPropertyName("points")] decimal? Points,
        [property: JsonPropertyName("blocked_by")] IReadOnlyList<string> BlockedBy);
}
