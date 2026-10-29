using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Status;

// Dates are yyyy-MM-dd. DaysLeft is set only while the sprint is running. Capacity is the team's entered total (null when
// none is entered); Velocity and WorkingDays are the sprint's stored velocity (null on an old points-only sprint) and its working-day count. Trend compares the last burndown point with the ideal line: ahead, on_track or behind.
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
    [property: JsonPropertyName("velocity")] decimal? Velocity,
    [property: JsonPropertyName("working_days")] int WorkingDays,
    [property: JsonPropertyName("capacity_members")] IReadOnlyList<SprintStatusResult.CapacityMember> CapacityMembers,
    [property: JsonPropertyName("trend")] string? Trend,
    [property: JsonPropertyName("burndown")] IReadOnlyList<SprintStatusResult.BurndownEntry> Burndown,
    [property: JsonPropertyName("states")] IReadOnlyList<SprintStatusResult.StateGroup> States,
    [property: JsonPropertyName("blocked")] IReadOnlyList<SprintStatusResult.StatusItem> Blocked,
    [property: JsonPropertyName("unparented_tasks")] IReadOnlyList<SprintStatusResult.StatusItem> UnparentedTasks) : McpResult(Project, OpenUrl)
{
    // Days is what the member has (their typed days, else the sprint's default); Points is their share of the capacity.
    internal sealed record CapacityMember(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("days")] decimal Days,
        [property: JsonPropertyName("points")] decimal Points);

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
