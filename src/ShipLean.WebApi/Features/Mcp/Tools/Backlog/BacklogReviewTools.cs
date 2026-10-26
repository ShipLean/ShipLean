using System.ComponentModel;
using System.Globalization;
using System.Text;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Backlog;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Status;

namespace ShipLean.WebApi.Features.Mcp.Tools.Backlog;

// The two read-only looks that prepare a planning session or a review: is the backlog in shape, and how did the sprint go.
[McpServerToolType]
internal sealed class BacklogReviewTools(
    ProjectResolver projects,
    SprintResolver sprints,
    IWorkTreeReads tree,
    ISprintMetrics metrics,
    BacklogReads reads,
    IAgentAddresses addresses,
    TimeProvider clock)
{
    private const int MaxTop = 100;

    [McpServerTool(Name = "review_backlog"), McpScope(McpScopes.Read)]
    [Description("Is the backlog in shape? Looks at the top of the stack of one level (stories, features or epics) and flags what is unestimated (stories and bugs), has no acceptance criteria (stories), has no parent (below epics), has not changed in a while, or looks like another item's duplicate (titles that share most of their words). Use it before planning a sprint.")]
    public async Task<CallToolResult> ReviewBacklog(
        [Description("The project key, such as SHIP. Leave out for the person's most recent project.")] string? project = null,
        [Description("stories (stories and bugs), features or epics. Defaults to stories.")] string level = "stories",
        [Description("How many items from the top of the stack to look at, 1 to 100. Defaults to 25.")] int top = 25,
        [Description("An item is stale when it has not changed for this many days. Defaults to 30.")] int stale_days = 30,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        if (top is < 1 or > MaxTop)
        {
            throw ApiException.Validation($"top must be between 1 and {MaxTop}.", "top");
        }
        if (stale_days < 1)
        {
            throw ApiException.Validation("stale_days must be at least 1.", "stale_days");
        }

        var stackLevel = (level ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "stories" or "story" => StackLevel.Requirement,
            "features" or "feature" => StackLevel.Feature,
            "epics" or "epic" => StackLevel.Epic,
            _ => throw ApiException.Validation($"\"{level}\" is not a level. Use stories, features or epics.", "level"),
        };
        var stack = await tree.StackAsync(resolved.Id, stackLevel, null, includeCompleted: false, cancellationToken);
        var findings = BacklogReviewer.Review(stack, clock.GetUtcNow(), stale_days, top);

        var counts = new FlagCounts(Count(findings, "unestimated"), Count(findings, "no_acceptance_criteria"), Count(findings, "no_parent"), Count(findings, "stale"), Count(findings, "likely_duplicate"));
        var levelName = stackLevel switch { StackLevel.Feature => "features", StackLevel.Epic => "epics", _ => "stories" };
        var result = new ReviewBacklogResult(
            resolved.Key, McpOpenUrls.Backlog((await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer, resolved.Key, levelName),
            levelName, stale_days, findings.Count, counts,
            [.. findings.Select(f => new ReviewedItem(f.Key, f.Title, f.Type, f.State, f.Points, f.DaysSinceChange, f.Flags, f.Duplicates))]);
        return StatusReply.Of(result, $"Top {findings.Count} {levelName} of {resolved.Key}: {counts.Unestimated} unestimated, {counts.NoAcceptanceCriteria} without acceptance criteria, {counts.NoParent} without a parent, {counts.Stale} stale, {counts.LikelyDuplicate} likely duplicates.");
    }

    [McpServerTool(Name = "sprint_review"), McpScope(McpScopes.Read)]
    [Description("What the sprint review and the retro need: points completed against committed (the sprint metrics' numbers), the stories carried over unfinished, the ones added after the sprint began, the velocity of the last few ended sprints, and how the features the sprint worked on are doing. Comes as data and as plain text sections to paste into a doc.")]
    public async Task<CallToolResult> SprintReview(
        [Description("The project key, such as SHIP. Leave out for the most recent project.")] string? project = null,
        [Description("current, next, or a sprint name. Defaults to current; name the sprint that just ended to review it.")] string sprint = "current",
        [Description("How many ended sprints the velocity trend shows, 1 to 12. Defaults to 3.")] int velocity_sprints = 3,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        if (velocity_sprints is < 1 or > 12)
        {
            throw ApiException.Validation("velocity_sprints must be between 1 and 12.", "velocity_sprints");
        }

        var resolvedSprint = await sprints.ResolveAsync(resolved.Id, sprint, cancellationToken: cancellationToken);
        var sprintMetrics = await metrics.GetSprintAsync(resolvedSprint.Id, cancellationToken);
        var outcomes = await metrics.GetSprintItemsAsync(resolvedSprint.Id, cancellationToken);
        var velocity = (await metrics.GetVelocityAsync(resolved.Id, cancellationToken)).Sprints.TakeLast(velocity_sprints)
            .Select(v => new VelocityPoint(v.SprintName, v.Committed, v.Completed)).ToList();
        var features = await FeatureLinesAsync(resolved.Id, resolvedSprint.Id, cancellationToken);

        var carried = outcomes.Where(o => o.CarriedOver).Select(o => new ReviewStory(o.Key, o.Title, o.Points)).ToList();
        var added = outcomes.Where(o => o.AddedMidSprint).Select(o => new ReviewStory(o.Key, o.Title, o.Points)).ToList();
        var sections = new List<ReviewSection>
        {
            new("Completed against committed", $"{Points(sprintMetrics.Completed)} of {Points(sprintMetrics.Committed)} committed points completed; {Points(sprintMetrics.Remaining)} remaining."),
            new("Carried over", List(carried, "Nothing is carried over.")),
            new("Added mid-sprint", List(added, "Nothing was added after the sprint began.")),
            new("Velocity", velocity.Count == 0 ? "No sprint has ended yet." : string.Join('\n', velocity.Select(v => $"{v.Sprint}: {Points(v.Completed)} completed of {Points(v.Committed)} committed"))),
            new("Feature progress", features.Count == 0 ? "No feature has stories in this sprint." : string.Join('\n', features.Select(f => $"{f.Key} {f.Title}: {f.Closed} closed, {f.Open} open" + (f.PointsTotal is { } total ? $", {Points(f.PointsDone ?? 0)} of {Points(total)} points" : string.Empty)))),
        };

        var result = new SprintReviewResult(
            resolved.Key, McpOpenUrls.SprintBacklog((await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer, resolved.Key, resolvedSprint.Id),
            resolvedSprint.Name, sprintMetrics.Committed, sprintMetrics.Completed, sprintMetrics.Remaining, carried, added, velocity, features, sections);
        return StatusReply.Of(result, $"{resolvedSprint.Name} ({resolved.Key}): {Points(sprintMetrics.Completed)} of {Points(sprintMetrics.Committed)} points completed, {carried.Count} carried over, {added.Count} added mid-sprint.");
    }

    private async Task<List<FeatureLine>> FeatureLinesAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken)
    {
        var features = await reads.FeaturesAsync(sprintId, cancellationToken);
        if (features.Count == 0)
        {
            return [];
        }

        var rollups = await tree.RollupsAsync(projectId, [.. features.Select(f => f.Id)], null, cancellationToken);
        return [.. features.Select(feature =>
        {
            var own = rollups.Where(r => r.ParentId == feature.Id && r.Type is WorkItemTypeId.Story or WorkItemTypeId.Bug).ToList();
            var total = own.Any(r => r.TotalPoints is not null) ? own.Sum(r => r.TotalPoints ?? 0) : (decimal?)null;
            var done = total is null ? null : total - own.Sum(r => r.OpenPoints ?? 0);
            return new FeatureLine(feature.Key, feature.Title, own.Sum(r => r.Open), own.Sum(r => r.Closed), done, total);
        })];
    }

    private static int Count(IReadOnlyList<BacklogFinding> findings, string flag) => findings.Count(f => f.Flags.Contains(flag));

    private static string Points(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string List(IReadOnlyList<ReviewStory> stories, string none)
    {
        if (stories.Count == 0)
        {
            return none;
        }

        var text = new StringBuilder();
        foreach (var story in stories)
        {
            text.Append(CultureInfo.InvariantCulture, $"{story.Key} {story.Title}{(story.Points is { } p ? $" ({Points(p)} points)" : string.Empty)}\n");
        }

        return text.ToString().TrimEnd();
    }
}
