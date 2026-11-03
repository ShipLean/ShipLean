using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Planning.Lessons;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Retros.Decide;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Plan;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

// Decides a scope's undecided retro points in one go: action items, Not needed, themes, merge proposals. Without `plan` it only previews and
// returns a plan; with the plan it applies exactly what the plan holds. Applying needs shiplean:plan; a preview needs only shiplean:read.
[McpServerToolType]
internal sealed class DecideRetroActionsTools(ItemResolver items, ProjectResolver projects, SprintResolver sprints, IProjectAccess access, CaptureSupport support, FeatureKind features, PlanEnvelope envelope, IRetroDecidePlanner planner, ICurrentUser user, ICurrentAgent agent)
{
    private const string PlanKind = "decide_retro_actions";
    private const int MaxNewActions = 3;

    [McpServerTool(Name = "decide_retro_actions"), McpScope(McpScopes.Plan, PreviewScope = McpScopes.Read)]
    [Description("Decide what to do with the undecided retrospective points in one scope (a Feature, a sprint, or a date range): create action items over them, add them to open actions, mark points Not needed with a reason, set themes, and propose theme merges for a person to accept. Give exactly one scope. Without `plan` it only previews and returns a plan; call again with that plan to apply exactly it. At most 3 new action items per call. A point already decided is skipped, never an error. A merge is only ever proposed; a person decides it.")]
    public async Task<CallToolResult> DecideRetroActions(
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("Scope: a Feature's key. Its own retro plus its stories' and bugs'.")] string? feature = null,
        [Description("Scope: current, next or a sprint name.")] string? sprint = null,
        [Description("Scope: the first day of a date range (yyyy-MM-dd, UTC, inclusive). Give with `to`.")] string? from = null,
        [Description("Scope: the last day of a date range (yyyy-MM-dd, UTC, inclusive). Give with `from`.")] string? to = null,
        [Description("Action items to create, or to add points to when `existing` names an open action.")] IReadOnlyList<DecideActionInput>? actions = null,
        [Description("Points that need no action, each with a reason.")] IReadOnlyList<DecideNotNeededInput>? not_needed = null,
        [Description("Themes to set on points. A theme a person chose is never overwritten.")] IReadOnlyList<DecideThemeInput>? themes = null,
        [Description("Pairs of themes to propose merging. Never applied: a person accepts or keeps them apart.")] IReadOnlyList<DecideMergeInput>? merges = null,
        [Description("The plan a preview returned. Applies exactly what it holds and ignores every other argument.")] string? plan = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: true, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);
        var openUrl = McpOpenUrls.Retros(await support.BaseUrlAsync(cancellationToken), resolved.Key);

        var actor = new RetroActor(user.Id, AgentStamp.Of(agent));
        if (plan is not null)
        {
            var applied = await planner.ApplyAsync(resolved.Id, envelope.Open<DecidePlan>(PlanKind, plan, resolved.Id), actor, cancellationToken);
            var done = Result(resolved.Key, openUrl, true, applied, null, null);
            return PlanningReply.Of(done, $"Applied: {applied.Created.Count} action items created, {applied.WouldAdd.Count} added to, {applied.WouldMarkNotNeeded.Count} marked Not needed, {applied.Skipped.Count} skipped, {applied.LeftUndecided} points still undecided.");
        }

        var scope = await RequireOneScopeAsync(resolved.Id, feature, sprint, from, to, cancellationToken);
        RequireFields(actions, not_needed, themes, merges);
        if (actions?.Count(action => string.IsNullOrWhiteSpace(action.Existing)) > MaxNewActions)
        {
            throw ApiException.Validation($"At most {MaxNewActions} new action items per call. Fold the points into fewer actions, or add them to an open action with `existing`.", "actions");
        }

        var request = new DecidePlan(
            scope,
            await PlanActionsAsync(resolved.Id, actions, cancellationToken),
            [.. (not_needed ?? []).Select(entry => new DecidePlanNotNeeded(entry.Point, entry.Reason))],
            [.. (themes ?? []).Select(entry => new DecidePlanTheme(entry.Point, entry.Theme))],
            [.. (merges ?? []).Select(entry => new DecidePlanMerge(entry.From, entry.Into, entry.Reason))]);
        var outcome = await planner.PreviewAsync(resolved.Id, request, cancellationToken);
        var preview = Result(resolved.Key, openUrl, false, outcome, envelope.Seal(PlanKind, resolved.Id, outcome.Plan), (int)PlanEnvelope.Lifetime.TotalMinutes);
        return PlanningReply.Of(preview, $"Preview: {outcome.WouldCreate.Count} action items would be created, {outcome.WouldAdd.Count} added to, {outcome.WouldMarkNotNeeded.Count} marked Not needed, {outcome.Skipped.Count} skipped, {outcome.LeftUndecided} points left undecided. Nothing was written; call decide_retro_actions again with the `plan` value from the JSON below to apply it.");
    }

    private static DecideRetroActionsResult Result(string project, string openUrl, bool applied, DecideOutcome outcome, string? plan, int? expiresInMinutes)
        => new(
            project, openUrl, applied,
            [.. outcome.WouldCreate.Select(line => new DecideWouldCreateLine(line.Title, line.FixType.ToString().ToLowerInvariant(), line.Theme, line.Covers, [.. line.PossibleDuplicates.Select(open => new DecideDuplicateLine(open.Key, open.Title, open.State))]))],
            [.. outcome.WouldAdd.Select(line => new DecideWouldAddLine(line.Existing, line.Covers))],
            [.. outcome.WouldMarkNotNeeded.Select(line => new DecideWouldNotNeededLine(line.Point, line.Text, line.Reason))],
            outcome.WouldTheme, outcome.WouldProposeMerges,
            [.. outcome.Skipped.Select(skip => new DecideSkippedLine(skip.Point, skip.Reason))],
            outcome.LeftUndecided, outcome.Created, plan, expiresInMinutes);

    private async Task<IReadOnlyList<DecidePlanAction>> PlanActionsAsync(Guid projectId, IReadOnlyList<DecideActionInput>? actions, CancellationToken cancellationToken)
    {
        var planned = new List<DecidePlanAction>();
        for (var index = 0; index < (actions?.Count ?? 0); index++)
        {
            var action = actions![index];
            Guid? existing = string.IsNullOrWhiteSpace(action.Existing)
                ? null
                : (await items.ResolveAsync(action.Existing, ProjectRole.Member, projectId, $"actions[{index}].existing", cancellationToken)).Id;
            planned.Add(new DecidePlanAction(
                action.Title,
                Enum.Parse<RetroFixType>(action.FixType, ignoreCase: true),
                action.Points,
                string.IsNullOrWhiteSpace(action.Theme) ? null : action.Theme.Trim(),
                existing));
        }

        return planned;
    }

    // The schema marks these fields required, but the generator lists every string as nullable, so a null or blank one gets here.
    private static void RequireFields(
        IReadOnlyList<DecideActionInput>? actions, IReadOnlyList<DecideNotNeededInput>? notNeeded, IReadOnlyList<DecideThemeInput>? themes, IReadOnlyList<DecideMergeInput>? merges)
    {
        for (var i = 0; i < (actions?.Count ?? 0); i++)
        {
            var action = actions![i];
            RequireText(action?.Title, $"actions[{i}].title");
            RequireText(action!.FixType, $"actions[{i}].fix_type");
            if (!Enum.TryParse<RetroFixType>(action.FixType, ignoreCase: true, out _))
            {
                throw ApiException.Validation("fix_type is skill, guide, test, analyzer, product or other.", $"actions[{i}].fix_type");
            }

            if (action.Points is not { Count: > 0 })
            {
                throw ApiException.Validation("Name at least one point.", $"actions[{i}].points");
            }
        }

        for (var i = 0; i < (notNeeded?.Count ?? 0); i++)
        {
            RequireText(notNeeded![i]?.Reason, $"not_needed[{i}].reason");
        }

        for (var i = 0; i < (themes?.Count ?? 0); i++)
        {
            RequireText(themes![i]?.Theme, $"themes[{i}].theme");
        }

        for (var i = 0; i < (merges?.Count ?? 0); i++)
        {
            RequireText(merges![i]?.From, $"merges[{i}].from");
            RequireText(merges[i].Into, $"merges[{i}].into");
            RequireText(merges[i].Reason, $"merges[{i}].reason");
        }
    }

    private static void RequireText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw ApiException.Validation($"{field} is required.", field);
        }
    }

    private async Task<DecideScope> RequireOneScopeAsync(Guid projectId, string? feature, string? sprint, string? from, string? to, CancellationToken cancellationToken)
    {
        var hasFeature = !string.IsNullOrWhiteSpace(feature);
        var hasSprint = !string.IsNullOrWhiteSpace(sprint);
        var hasFrom = !string.IsNullOrWhiteSpace(from);
        var hasTo = !string.IsNullOrWhiteSpace(to);
        if (hasFrom != hasTo)
        {
            throw ApiException.Validation("Give `from` and `to` together, or neither.", "scope");
        }

        if (new[] { hasFeature, hasSprint, hasFrom }.Count(given => given) != 1)
        {
            throw ApiException.Validation("Give exactly one scope: `feature`, `sprint`, or `from` with `to`.", "scope");
        }

        if (hasFeature)
        {
            var resolvedFeature = await items.ResolveAsync(feature!, ProjectRole.Member, projectId, "feature", cancellationToken);
            if (!await features.IsFeatureAsync(resolvedFeature.Id, cancellationToken))
            {
                throw ApiException.Validation($"{resolvedFeature.Key} is not a Feature. Give the key of a Feature.", "feature");
            }

            return new DecideScope(resolvedFeature.Id, null, null, null);
        }

        if (hasSprint)
        {
            return new DecideScope(null, (await sprints.ResolveAsync(projectId, sprint!, cancellationToken: cancellationToken)).Id, null, null);
        }

        var start = Day(from!, "from");
        var end = Day(to!, "to");
        return start > end ? throw ApiException.Validation("`from` is after `to`.", "from") : new DecideScope(null, null, start, end);
    }

    private static DateOnly Day(string text, string field)
        => DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : throw ApiException.Validation($"\"{text}\" is not a date like 2026-11-03.", field);
}
