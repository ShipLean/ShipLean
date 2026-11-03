using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.Application.Features.Retros.Decide;

// Decide what to do with a scope's points (K8). A preview reads and writes nothing. An apply does what the plan holds and nothing else, but re-reads
// what is decided first: a point decided since the preview is skipped and the rest goes ahead. A merge is only ever proposed, a person accepts it.
internal sealed class RetroDecidePlanner(DecideScopeReader reader, IRetroDecisionWrites writes) : IRetroDecidePlanner
{
    private const string AlreadyDecided = "Already decided.";
    private const string ThemeByPerson = "The theme was set by a person.";
    private const string NoLongerInScope = "The point is no longer in the scope.";
    private const string ActionNotLive = "The action item is no longer open.";

    public async Task<DecideOutcome> PreviewAsync(Guid projectId, DecidePlan request, CancellationToken cancellationToken)
    {
        var points = await reader.PointsAsync(projectId, request.Scope, cancellationToken);
        Validate(request, points);
        var open = await reader.OpenActionsAsync(projectId, cancellationToken);
        var skipped = new Skips();
        var themeOf = request.Themes.ToDictionary(theme => theme.PointId, theme => theme.Theme);

        var create = new List<DecideCreateLine>();
        var add = new List<DecideAddLine>();
        var planActions = new List<DecidePlanAction>();
        var handled = new HashSet<Guid>();
        for (var index = 0; index < request.Actions.Count; index++)
        {
            var action = request.Actions[index];
            var kept = Undecided(action.PointIds, points, skipped);
            if (kept.Count == 0)
            {
                continue;
            }

            kept.ForEach(id => handled.Add(id));
            planActions.Add(action with { PointIds = kept });
            if (action.Existing is { } existingId)
            {
                var existing = await reader.LiveActionAsync(projectId, existingId, cancellationToken)
                    ?? throw ApiException.Validation("That is not a live action item raised from a retro.", $"actions[{index}].existing");
                add.Add(new DecideAddLine(existing.Key, kept.Count));
                continue;
            }

            var theme = action.Theme ?? Commonest(kept.Select(id => themeOf.TryGetValue(id, out var given) && !points[id].ThemeByPerson ? given : points[id].ThemeName));
            create.Add(new DecideCreateLine(action.Title.Trim(), action.FixType, theme, kept.Count, RetroDuplicates.Find(action.Title, open)));
        }

        var notNeeded = new List<DecideNotNeededLine>();
        var planNotNeeded = new List<DecidePlanNotNeeded>();
        foreach (var entry in request.NotNeeded)
        {
            if (Undecided([entry.PointId], points, skipped).Count == 0)
            {
                continue;
            }

            handled.Add(entry.PointId);
            planNotNeeded.Add(entry);
            notNeeded.Add(new DecideNotNeededLine(entry.PointId, points[entry.PointId].Text, entry.Reason.Trim()));
        }

        var planThemes = new List<DecidePlanTheme>();
        foreach (var entry in request.Themes)
        {
            var point = points[entry.PointId];
            if (point.Decided)
            {
                skipped.Add(entry.PointId, AlreadyDecided);
            }
            else if (point.ThemeByPerson)
            {
                skipped.Add(entry.PointId, ThemeByPerson);
            }
            else
            {
                planThemes.Add(entry);
            }
        }

        var merges = await NewMergesAsync(projectId, request.Merges, cancellationToken);
        var plan = new DecidePlan(request.Scope, planActions, planNotNeeded, planThemes, merges);
        var left = points.Values.Count(point => point.Undecided && !handled.Contains(point.Id));
        return new DecideOutcome(create, add, notNeeded, planThemes.Count, merges.Count, skipped.Lines, left, [], plan);
    }

    public async Task<DecideOutcome> ApplyAsync(Guid projectId, DecidePlan plan, RetroActor actor, CancellationToken cancellationToken)
    {
        var points = await reader.PointsAsync(projectId, plan.Scope, cancellationToken);
        var skipped = new Skips();
        var themed = 0;
        foreach (var entry in plan.Themes)
        {
            if (!points.TryGetValue(entry.PointId, out var point))
            {
                skipped.Add(entry.PointId, NoLongerInScope);
            }
            else if (point.Decided)
            {
                skipped.Add(entry.PointId, AlreadyDecided);
            }
            else if (await writes.AssignThemeAsync(entry.PointId, entry.Theme, actor, cancellationToken))
            {
                themed++;
            }
            else
            {
                skipped.Add(entry.PointId, ThemeByPerson);
            }
        }

        var create = new List<DecideCreateLine>();
        var add = new List<DecideAddLine>();
        var notNeeded = new List<DecideNotNeededLine>();
        var created = new List<string>();
        foreach (var entry in plan.NotNeeded)
        {
            if (Undecided([entry.PointId], points, skipped).Count == 0)
            {
                continue;
            }

            try
            {
                await writes.MarkNotNeededAsync(entry.PointId, new RetroNotNeededPut(entry.Reason), actor, cancellationToken);
                notNeeded.Add(new DecideNotNeededLine(entry.PointId, points[entry.PointId].Text, entry.Reason.Trim()));
            }
            catch (ApiException exception) when (exception.Code == ApiErrorCodes.Conflict)
            {
                skipped.Add(entry.PointId, AlreadyDecided);
            }
        }

        foreach (var action in plan.Actions)
        {
            var kept = Undecided(action.PointIds, points, skipped);
            // A point decided between the read and the write makes the writer conflict. Read again and retry with the points still undecided.
            // Each retry follows a smaller set, so the loop ends; a conflict that drops no point is a real one and propagates.
            while (kept.Count > 0)
            {
                try
                {
                    await ApplyActionAsync(projectId, action, kept, actor, skipped, create, add, created, cancellationToken);
                    break;
                }
                catch (ApiException exception) when (exception.Code == ApiErrorCodes.Conflict)
                {
                    points = await reader.PointsAsync(projectId, plan.Scope, cancellationToken);
                    var remaining = Undecided(kept, points, skipped);
                    if (remaining.Count == kept.Count)
                    {
                        throw;
                    }

                    kept = remaining;
                }
            }
        }

        var proposed = 0;
        foreach (var merge in plan.Merges)
        {
            if (await writes.ProposeMergeAsync(projectId, merge.From, merge.Into, merge.Reason, actor, cancellationToken))
            {
                proposed++;
            }
        }

        var left = (await reader.PointsAsync(projectId, plan.Scope, cancellationToken)).Values.Count(point => point.Undecided);
        return new DecideOutcome(create, add, notNeeded, themed, proposed, skipped.Lines, left, created, plan);
    }

    // The scope check, over every point the request names: all must be in the scope and none wentWell; an action or Not needed entry owns its points alone.
    private static void Validate(DecidePlan request, IReadOnlyDictionary<Guid, DecideScopePoint> points)
    {
        var decided = new HashSet<Guid>();
        for (var index = 0; index < request.Actions.Count; index++)
        {
            foreach (var id in request.Actions[index].PointIds)
            {
                RequirePoint(id, points, $"actions[{index}].points");
                RequireFirstUse(decided, id, $"actions[{index}].points");
            }
        }

        for (var index = 0; index < request.NotNeeded.Count; index++)
        {
            var id = request.NotNeeded[index].PointId;
            RequirePoint(id, points, $"not_needed[{index}].point");
            RequireFirstUse(decided, id, $"not_needed[{index}].point");
        }

        var themed = new HashSet<Guid>();
        for (var index = 0; index < request.Themes.Count; index++)
        {
            var id = request.Themes[index].PointId;
            RequirePoint(id, points, $"themes[{index}].point");
            RequireFirstUse(themed, id, $"themes[{index}].point");
        }
    }

    private static void RequirePoint(Guid id, IReadOnlyDictionary<Guid, DecideScopePoint> points, string field)
    {
        if (!points.TryGetValue(id, out var point))
        {
            throw ApiException.Validation($"Point {id} is not in the scope you gave.", field);
        }

        if (point.Category == RetroCategory.WentWell)
        {
            throw ApiException.Validation($"Point {id} is a went-well point; those need no decision.", field);
        }
    }

    private static void RequireFirstUse(HashSet<Guid> seen, Guid id, string field)
    {
        if (!seen.Add(id))
        {
            throw ApiException.Validation($"Point {id} is named twice. Give each point once.", field);
        }
    }

    // The points still undecided, in the order given; the others go to skipped.
    private async Task ApplyActionAsync(
        Guid projectId, DecidePlanAction action, List<Guid> kept, RetroActor actor, Skips skipped,
        List<DecideCreateLine> create, List<DecideAddLine> add, List<string> created, CancellationToken cancellationToken)
    {
        if (action.Existing is { } existingId)
        {
            var existing = await reader.LiveActionAsync(projectId, existingId, cancellationToken);
            if (existing is null)
            {
                kept.ForEach(id => skipped.Add(id, ActionNotLive));
                return;
            }

            await writes.AddPointsAsync(existingId, new RetroActionAddPoints(kept), actor, cancellationToken);
            add.Add(new DecideAddLine(existing.Key, kept.Count));
            return;
        }

        var detail = await writes.CreateActionAsync(projectId, new RetroActionNew(action.Title, action.FixType, kept, null, action.Theme), actor, cancellationToken);
        created.Add(detail.Action.Key);
        create.Add(new DecideCreateLine(detail.Action.Title, action.FixType, detail.Theme?.Name, kept.Count, []));
    }

    private static List<Guid> Undecided(IReadOnlyList<Guid> ids, IReadOnlyDictionary<Guid, DecideScopePoint> points, Skips skipped)
    {
        var kept = new List<Guid>();
        foreach (var id in ids)
        {
            if (!points.TryGetValue(id, out var point))
            {
                skipped.Add(id, NoLongerInScope);
            }
            else if (point.Undecided)
            {
                kept.Add(id);
            }
            else
            {
                skipped.Add(id, AlreadyDecided);
            }
        }

        return kept;
    }

    // The commonest theme; on a tie the first met, as the writer picks it.
    private static string? Commonest(IEnumerable<string?> names)
        => names.OfType<string>().GroupBy(name => name, StringComparer.OrdinalIgnoreCase).MaxBy(group => group.Count())?.Key;

    // Merges that name two different themes of the project and have no proposal yet, each pair once.
    private async Task<List<DecidePlanMerge>> NewMergesAsync(Guid projectId, IReadOnlyList<DecidePlanMerge> merges, CancellationToken cancellationToken)
    {
        var themes = await reader.ThemesAsync(projectId, [.. merges.SelectMany(merge => new[] { merge.From, merge.Into })], cancellationToken);
        var seen = new HashSet<(Guid, Guid)>();
        var fresh = new List<DecidePlanMerge>();
        for (var index = 0; index < merges.Count; index++)
        {
            var merge = merges[index];
            if (!themes.TryGetValue(merge.From.Trim().ToLowerInvariant(), out var from))
            {
                throw ApiException.Validation("No theme with that name was found.", $"merges[{index}].from");
            }

            if (!themes.TryGetValue(merge.Into.Trim().ToLowerInvariant(), out var into))
            {
                throw ApiException.Validation("No theme with that name was found.", $"merges[{index}].into");
            }

            if (from == into)
            {
                throw ApiException.Validation("Pick two different themes.", $"merges[{index}].into");
            }

            var pair = from.CompareTo(into) < 0 ? (from, into) : (into, from);
            if (seen.Add(pair) && !await reader.MergePairExistsAsync(from, into, cancellationToken))
            {
                fresh.Add(merge);
            }
        }

        return fresh;
    }

    private sealed class Skips
    {
        private readonly List<DecideSkip> _lines = [];
        private readonly HashSet<(Guid, string)> _seen = [];

        public IReadOnlyList<DecideSkip> Lines => _lines;

        public void Add(Guid point, string reason)
        {
            if (_seen.Add((point, reason)))
            {
                _lines.Add(new DecideSkip(point, reason));
            }
        }
    }
}
