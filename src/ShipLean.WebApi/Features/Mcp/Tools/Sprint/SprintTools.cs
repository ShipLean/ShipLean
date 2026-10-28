using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Sprint;
using ShipLean.Application.Features.Mcp.Status;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.Core;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Plan;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Status;

namespace ShipLean.WebApi.Features.Mcp.Tools.Sprint;

// The product owner's sprint-planning tools. plan_sprint and end_sprint preview first and apply exactly what the preview
// sealed; create_sprint writes straight away. Applying needs shiplean:plan; a preview needs only shiplean:read.
[McpServerToolType]
internal sealed class SprintTools(
    ProjectResolver projects,
    SprintResolver sprints,
    IProjectAccess access,
    IWorkTreeReads tree,
    IWorkItemStore store,
    ISprintStore sprintStore,
    ISprintMetrics metrics,
    SprintStatusReads heads,
    SprintPlanReads planReads,
    PlanEnvelope envelope,
    IAgentAddresses addresses,
    TimeProvider clock)
{
    private const string PlanKind = "plan_sprint";
    private const string EndKind = "end_sprint";
    private const int NextShown = 5;
    private const int DefaultLengthDays = 14;

    [McpServerTool(Name = "plan_sprint"), McpScope(McpScopes.Plan, PreviewScope = McpScopes.Read)]
    [Description("Fill a sprint from the top of the backlog. Without `plan` it only proposes: the unplanned stories and bugs, in stack order, that fit the sprint's room (the team's entered capacity, else the velocity; items already in the sprint count against it), and a plan. Call again with that plan to move exactly those items in. Going over capacity warns and never blocks. A sprint with no capacity and no velocity (a first sprint) gets no cut line: pass points_limit to get one.")]
    public async Task<CallToolResult> PlanSprint(
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("next, current or a sprint name. Defaults to next.")] string? sprint = null,
        [Description("Fill up to this many points instead of the capacity or velocity.")] decimal? points_limit = null,
        [Description("The plan a preview returned. Moves exactly what it holds into the sprint.")] string? plan = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: true, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);

        if (plan is not null)
        {
            var sealedPlan = envelope.Open<PlanSprintPlan>(PlanKind, plan, resolved.Id);
            return await ApplyPlanAsync(resolved, sealedPlan, cancellationToken);
        }

        if (points_limit is < 0)
        {
            throw ApiException.Validation("points_limit cannot be negative.", "points_limit");
        }

        var target = await sprints.ResolveAsync(resolved.Id, sprint ?? "next", cancellationToken: cancellationToken);
        var head = await heads.HeadAsync(target.Id, cancellationToken);
        if (head.EndDate < SprintLabels.Today(clock))
        {
            throw ApiException.Validation($"{head.Name} has already ended; plan a sprint that is running or still to come.", "sprint");
        }

        var (room, source) = await RoomAsync(target.Id, resolved.Id, points_limit, cancellationToken);
        var stack = await tree.StackAsync(resolved.Id, StackLevel.Requirement, null, includeCompleted: true, cancellationToken);
        var alreadyIn = stack.Where(r => r.Sprint?.Id == target.Id && r.State.Category != StateCategory.Removed).Sum(r => r.Points ?? 0m);
        var candidates = stack.Where(r => r.Sprint is null && r.State.Category is not (StateCategory.Completed or StateCategory.Removed)).ToList();
        var url = McpOpenUrls.SprintBacklog((await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer, resolved.Key, target.Id);

        if (room is null)
        {
            var noCut = new PlanSprintResult(
                resolved.Key, url, head.Name, false, null, null, alreadyIn, [], 0m, [.. candidates.Take(NextShown * 2).Select(Line)],
                [$"{head.Name} has no capacity entered and there is no velocity yet, so there is no cut line."], true, null, null);
            return SprintReply.Of(noCut, $"No capacity or velocity for {head.Name}: pass points_limit to get a proposal. The top of the stack is listed.");
        }

        var fill = SprintFill.Cut(candidates, room.Value, alreadyIn);
        var warnings = new List<string>();
        if (fill.OverBy is { } over)
        {
            warnings.Add($"{head.Name} would hold {fill.SprintPoints} points, {over} over its room of {room}.");
        }
        if (fill.Capped)
        {
            warnings.Add($"Stopped at {SprintFill.MaxItems} items; plan again after applying for more.");
        }
        var unestimated = fill.Selected.Where(r => r.Points is null).Select(r => r.Key).ToList();
        if (unestimated.Count > 0)
        {
            warnings.Add($"Unestimated, counted as zero points: {string.Join(", ", unestimated)}.");
        }

        var points = fill.Selected.Sum(r => r.Points ?? 0m);
        var sealedItems = fill.Selected.Select(r => new PlanItem(r.Id, r.Revision)).ToList();
        var preview = new PlanSprintResult(
            resolved.Key, url, head.Name, false, room, source, alreadyIn, [.. fill.Selected.Select(Line)], points,
            [.. fill.Left.Take(NextShown).Select(Line)], warnings,
            false, envelope.Seal(PlanKind, resolved.Id, new PlanSprintPlan(target.Id, sealedItems)), (int)PlanEnvelope.Lifetime.TotalMinutes);
        return SprintReply.Of(preview, $"Preview: {fill.Selected.Count} items ({points} points) into {head.Name}, room {room}. Nothing moved yet; call plan_sprint again with the `plan` value from the JSON below to apply it.");
    }

    [McpServerTool(Name = "end_sprint"), McpScope(McpScopes.Plan, PreviewScope = McpScopes.Read)]
    [Description("Close a sprint: the roll-over prompt as a tool. Without `plan` it previews which unfinished stories and bugs move to the next sprint (or the backlog) and returns a plan; call again with that plan to move them. The roll-over then shows as decided for everyone. A sprint can be closed from its end day on; while it is still running (its end date is after today) pass end_today: true, which moves its end date to today first. An earlier sprint that has already ended is named by `sprint`.")]
    public async Task<CallToolResult> EndSprint(
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("current (the sprint running today) or a sprint name. Defaults to current.")] string? sprint = null,
        [Description("next (the default: the following sprint, or the backlog when there is none) or backlog.")] string? to = null,
        [Description("Close a sprint that is still running: its end date moves to today first.")] bool end_today = false,
        [Description("The plan a preview returned. Closes the sprint and moves exactly what it holds.")] string? plan = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: true, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);

        EndSprintPlan? sealedPlan = null;
        Guid sprintId;
        string destination;
        var endToday = end_today;
        if (plan is not null)
        {
            sealedPlan = envelope.Open<EndSprintPlan>(EndKind, plan, resolved.Id);
            sprintId = sealedPlan.SprintId;
            destination = sealedPlan.To;
            endToday = sealedPlan.EndToday;
        }
        else
        {
            destination = ParseTo(to);
            sprintId = (await sprints.ResolveAsync(resolved.Id, sprint ?? "current", cancellationToken: cancellationToken)).Id;
        }

        var head = await heads.HeadAsync(sprintId, cancellationToken);
        var today = SprintLabels.Today(clock);
        var movesEndDate = head.EndDate > today;
        if (movesEndDate && !endToday)
        {
            throw ApiException.Validation($"{head.Name} is still running (it ends {head.EndDate:yyyy-MM-dd}). Pass end_today: true to close it today: its end date moves to today and the unfinished items roll over.", "end_today");
        }
        if (movesEndDate && head.StartDate > today)
        {
            throw ApiException.Validation($"{head.Name} has not started (it starts {head.StartDate:yyyy-MM-dd}), so there is nothing to close.", "sprint");
        }

        var rollover = await sprintStore.GetRolloverAsync(sprintId, cancellationToken);
        if (rollover.Status == RolloverStatus.Decided)
        {
            throw ApiException.Validation($"{head.Name} has already been rolled over.", "sprint");
        }

        var url = McpOpenUrls.Sprints((await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer, resolved.Key);
        var targetName = destination == "backlog" || rollover.NextSprint is null ? "backlog" : rollover.NextSprint.Name;
        var current = rollover.Items.Select(i => (Item: i, Target: destination == "backlog" ? null : i.DefaultTargetSprintId)).ToList();
        var lines = current.Select(m => new SprintItemLine(m.Item.Key, m.Item.Title, m.Item.Type.ToString().ToLowerInvariant(), m.Item.Points, targetName)).ToList();
        var movedPoints = current.Sum(m => m.Item.Points ?? 0m);

        if (sealedPlan is not null)
        {
            await CheckUnchangedAsync(sealedPlan.Moves, [.. current.Select(m => (m.Item.WorkItemId, m.Target))], cancellationToken);
            if (movesEndDate)
            {
                await sprintStore.UpdateAsync(sprintId, new SprintUpdate(null, null, today, null), cancellationToken);
            }
            await sprintStore.RolloverAsync(sprintId, new RolloverRequest([.. current.Select(m => new RolloverMove(m.Item.WorkItemId, m.Target))]), cancellationToken);
            var done = new EndSprintResult(resolved.Key, url, head.Name, true, lines, movedPoints, [], null, null);
            return SprintReply.Of(done, $"{head.Name} is closed: {lines.Count} items ({movedPoints} points) moved to {targetName}.");
        }

        var warnings = new List<string>();
        if (movesEndDate)
        {
            warnings.Add($"{head.Name}'s end date will move from {head.EndDate:yyyy-MM-dd} to today, which shortens its burndown.");
        }
        if (destination != "backlog" && rollover.NextSprint is null && lines.Count > 0)
        {
            warnings.Add("There is no sprint after this one, so the items go back to the backlog.");
        }
        if (targetName != "backlog" && rollover.NextSprintCapacity is { } capacity && rollover.NextSprintPoints + movedPoints > capacity)
        {
            warnings.Add($"{targetName} would hold {rollover.NextSprintPoints + movedPoints} points against a capacity of {capacity}.");
        }

        var moves = new List<EndSprintMove>();
        foreach (var (item, target) in current)
        {
            moves.Add(new EndSprintMove(item.WorkItemId, (await store.GetAsync(item.WorkItemId, cancellationToken)).Revision, target));
        }
        var preview = new EndSprintResult(
            resolved.Key, url, head.Name, false, lines, movedPoints, warnings,
            envelope.Seal(EndKind, resolved.Id, new EndSprintPlan(sprintId, destination, endToday, moves)), (int)PlanEnvelope.Lifetime.TotalMinutes);
        return SprintReply.Of(preview, $"Preview: {lines.Count} unfinished items ({movedPoints} points) of {head.Name} would move to {targetName}. Nothing moved yet; call end_sprint again with the `plan` value from the JSON below to apply it.");
    }

    [McpServerTool(Name = "create_sprint"), McpScope(McpScopes.Plan)]
    [Description("Create the next sprint. The name continues the last sprint's number, it starts the day after the last one ends and runs for the same length; each can be given instead. The goal is optional.")]
    public async Task<CallToolResult> CreateSprint(
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("The sprint's name. Defaults to the last name with its number plus one.")] string? name = null,
        [Description("First day, yyyy-MM-dd. Defaults to the day after the last sprint ends (today for a first sprint).")] string? start_date = null,
        [Description("Last day, yyyy-MM-dd. Defaults to the same length as the last sprint (14 days for a first sprint).")] string? end_date = null,
        [Description("What the sprint is for. Optional.")] string? goal = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: true, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);

        var last = await planReads.LastAsync(resolved.Id, cancellationToken);
        var start = ParseDay(start_date, "start_date") ?? (last is null ? SprintLabels.Today(clock) : last.EndDate.AddDays(1));
        var length = last is null ? DefaultLengthDays - 1 : last.EndDate.DayNumber - last.StartDate.DayNumber;
        var end = ParseDay(end_date, "end_date") ?? start.AddDays(length);
        var sprintName = name?.Trim();
        if (string.IsNullOrEmpty(sprintName))
        {
            sprintName = last is null ? "Sprint 1" : SprintNames.Next(last.Name, last.Count);
        }

        var created = await sprintStore.CreateAsync(resolved.Id, new SprintCreate(sprintName, start, end, goal?.Trim() ?? string.Empty), null, cancellationToken);
        var result = new CreateSprintResult(
            resolved.Key, McpOpenUrls.Sprints((await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer, resolved.Key), created.Name,
            Day(created.StartDate), Day(created.EndDate), created.EndDate.DayNumber - created.StartDate.DayNumber + 1, created.Goal, last?.Name);
        return SprintReply.Of(result, $"Created {created.Name}: {result.StartDate} to {result.EndDate} ({result.LengthDays} days).");
    }

    private async Task<CallToolResult> ApplyPlanAsync(ResolvedProject project, PlanSprintPlan plan, CancellationToken cancellationToken)
    {
        var head = await heads.HeadAsync(plan.SprintId, cancellationToken);
        if (head.EndDate < SprintLabels.Today(clock))
        {
            throw ApiException.Validation($"{head.Name} has ended since the preview, so nothing was moved. Plan a sprint that is running or still to come.", "sprint");
        }
        var snapshots = new List<WorkItemSnapshot>();
        foreach (var item in plan.Items)
        {
            var snapshot = await store.GetAsync(item.Id, cancellationToken);
            if (snapshot.ProjectId != project.Id || snapshot.Revision != item.Revision || snapshot.SprintId is not null)
            {
                throw ApiException.Conflict($"{snapshot.Key} changed since the preview, so nothing was moved. Preview again, then apply the new plan.");
            }
            snapshots.Add(snapshot);
        }

        var changes = new WorkItemChanges { Sprint = new FieldSet<Guid?>(plan.SprintId) };
        var outcome = await store.PatchManyAsync([.. snapshots.Select(s => (s.Id, new WorkItemPatch(s.Revision, changes)))], WriteOptions.User, cancellationToken);
        if (outcome.Conflicts is { Count: > 0 } stale)
        {
            throw ApiException.Conflict($"{string.Join(", ", stale.Select(c => c.Current.Key))} changed since the preview, so nothing was moved. Preview again, then apply the new plan.");
        }

        var lines = snapshots.Select(s => new SprintItemLine(s.Key, s.Title, s.Type.ToString().ToLowerInvariant(), s.Points)).ToList();
        var points = snapshots.Sum(s => s.Points ?? 0m);
        var result = new PlanSprintResult(
            project.Key, McpOpenUrls.SprintBacklog((await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer, project.Key, plan.SprintId), head.Name,
            true, null, null, 0m, lines, points, [], [], false, null, null);
        return SprintReply.Of(result, $"Moved {lines.Count} items ({points} points) into {head.Name}.");
    }

    // The unfinished items, their destinations and their revisions must be exactly what the preview saw.
    private async Task CheckUnchangedAsync(IReadOnlyList<EndSprintMove> sealedMoves, IReadOnlyList<(Guid Id, Guid? Target)> now, CancellationToken cancellationToken)
    {
        var sealedNow = sealedMoves.Select(m => (m.Id, Target: m.TargetSprintId)).OrderBy(m => m.Id);
        if (!sealedNow.SequenceEqual(now.OrderBy(m => m.Id)))
        {
            throw ApiException.Conflict("The sprint's unfinished items or where they would go (the next sprint) changed since the preview, so nothing was moved. Preview again, then apply the new plan.");
        }
        foreach (var move in sealedMoves)
        {
            var snapshot = await store.GetAsync(move.Id, cancellationToken);
            if (snapshot.Revision != move.Revision)
            {
                throw ApiException.Conflict($"{snapshot.Key} changed since the preview, so nothing was moved. Preview again, then apply the new plan.");
            }
        }
    }

    // The points the sprint can take: an explicit limit, else the entered team capacity, else the velocity (its override first).
    private async Task<(decimal? Room, string? Source)> RoomAsync(Guid sprintId, Guid projectId, decimal? pointsLimit, CancellationToken cancellationToken)
    {
        if (pointsLimit is { } limit)
        {
            return (limit, "points_limit");
        }

        var capacity = await sprintStore.GetCapacityAsync(sprintId, cancellationToken);
        if (capacity is { Members.Count: > 0 })
        {
            return (capacity.TeamTotal, "capacity");
        }

        var velocity = await metrics.GetVelocityAsync(projectId, cancellationToken);
        var perSprint = velocity.Override ?? velocity.Average;
        return perSprint > 0 ? (perSprint, "velocity") : (null, null);
    }

    private static SprintItemLine Line(StackRowDto row) => new(row.Key, row.Title, row.Type.Id.ToString().ToLowerInvariant(), row.Points);

    private static string ParseTo(string? to)
    {
        var wanted = string.IsNullOrWhiteSpace(to) ? "next" : to.Trim().ToLowerInvariant();
        return wanted is "next" or "backlog" ? wanted : throw ApiException.Validation("`to` is next or backlog.", "to");
    }

    private static DateOnly? ParseDay(string? text, string field)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        return DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : throw ApiException.Validation($"\"{text}\" is not a date: use yyyy-MM-dd.", field);
    }

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
