using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Status;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Sprints;
using ShipLean.Core;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Tools.Status;

[McpServerToolType]
internal sealed class SprintStatusTools(
    ProjectResolver projects,
    SprintResolver sprints,
    SprintStatusReads reads,
    ISprintMetrics metrics,
    ISprintStore sprintStore,
    IAgentAddresses addresses,
    TimeProvider clock)
{
    [McpServerTool(Name = "sprint_status"), McpScope(McpScopes.Read)]
    [Description("The stand-up picture for a sprint: goal, dates, days left, points committed, completed and remaining against the team's capacity (its velocity, working days and each member's days), the burndown trend, every item by state with who has it, the blocked items (tagged Blocked or waiting on an open blocked-by link) and tasks with no story. Use it for 'how is the sprint going?'.")]
    public async Task<CallToolResult> SprintStatus(
        [Description("The project key, such as SHIP. Leave out for the most recent project.")] string? project = null,
        [Description("current, next, or a sprint name. Defaults to current.")] string sprint = "current",
        CancellationToken cancellationToken = default)
    {
        var resolvedProject = await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        var resolvedSprint = await sprints.ResolveAsync(resolvedProject.Id, sprint, cancellationToken: cancellationToken);
        var head = await reads.HeadAsync(resolvedSprint.Id, cancellationToken);
        var items = await reads.ItemsAsync(resolvedSprint.Id, cancellationToken);
        var sprintMetrics = await metrics.GetSprintAsync(resolvedSprint.Id, cancellationToken);
        var capacity = await sprintStore.GetCapacityAsync(resolvedSprint.Id, cancellationToken);
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;

        var today = SprintLabels.Today(clock);
        var running = SprintLabels.Phase(head.StartDate, head.EndDate, today) == SprintPhase.Current;
        var rows = items.Select(item => new SprintStatusResult.StatusItem(item.Key, item.Title, item.Type, item.State, item.Assignee, item.Points, item.BlockedBy)).ToList();
        var blocked = items.Zip(rows).Where(pair => pair.First.Blocked).Select(pair => pair.Second).ToList();
        var unparented = items.Zip(rows).Where(pair => pair.First.Unparented && pair.First.Type == "task").Select(pair => pair.Second).ToList();
        var states = items.Zip(rows)
            .GroupBy(pair => (pair.First.State, pair.First.StateCategory))
            .Select(group => new SprintStatusResult.StateGroup(group.Key.State, group.Key.StateCategory, [.. group.Select(pair => pair.Second)]))
            .ToList();
        var burndown = sprintMetrics.Burndown.Select(point => new SprintStatusResult.BurndownEntry(point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), point.Remaining, point.Ideal)).ToList();
        var last = sprintMetrics.Burndown.Count > 0 ? sprintMetrics.Burndown[^1] : null;
        var trend = last is null ? null : Trend(last);
        var timeLabel = SprintLabels.Time(head.StartDate, head.EndDate, today);

        var result = new SprintStatusResult(
            resolvedProject.Key, McpOpenUrls.Taskboard(issuer, resolvedProject.Key, resolvedSprint.Id),
            head.Name, head.Goal, head.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), head.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            running ? head.EndDate.DayNumber - today.DayNumber : null, timeLabel,
            sprintMetrics.Committed, sprintMetrics.Completed, sprintMetrics.Remaining,
            capacity.Members.Count > 0 ? capacity.TeamTotal : null, capacity.Velocity, capacity.WorkingDays,
            [.. capacity.Members.Select(m => new SprintStatusResult.CapacityMember(m.User.Name, m.Days ?? capacity.DefaultDays, m.Points))], trend, burndown, states, blocked, unparented);
        return StatusReply.Of(result, $"{head.Name} ({resolvedProject.Key}): {timeLabel}. {sprintMetrics.Completed} of {sprintMetrics.Committed} points done, {blocked.Count} blocked.");
    }

    private static string Trend(BurndownPoint last)
    {
        if (last.Remaining > last.Ideal)
        {
            return "behind";
        }
        return last.Remaining < last.Ideal ? "ahead" : "on_track";
    }
}
