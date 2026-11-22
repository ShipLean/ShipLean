using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.Retros.Decide;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Retros.Analysis;
using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.Application.Features.Retros.Analysis.Record;

// record_retro_analysis (C2). Preview validates, derives the funnel and evidence from the database, ranks, and asks the decide planner what the actions would do; it writes nothing.
// Apply does the same, runs the actions through the planner (each creates its own work item, so this is not one transaction with the analysis), then stores the analysis in one
// transaction. A point decided since the preview is skipped by the planner and the analysis is still stored.
internal sealed class RetroAnalysisWriter(
    DecideScopeReader scopeReader, RetroAnalysisReads reads, RetroAnalysisStore store, IRetroDecidePlanner planner, ILogger<RetroAnalysisWriter> logger) : IRetroAnalysisWriter
{
    private const int MaxListedPoints = 20;

    public async Task<RetroAnalysisOutcome> PreviewAsync(Guid projectId, RecordRetroAnalysisRequest request, CancellationToken cancellationToken)
    {
        var run = await PrepareAsync(projectId, request, strict: true, cancellationToken);
        var outcome = run.Plan.Actions.Count == 0 ? null : await planner.PreviewAsync(projectId, run.Plan, cancellationToken);
        var create = outcome?.WouldCreate ?? [];
        var add = outcome?.WouldAdd ?? [];
        return new RetroAnalysisOutcome(
            Funnel(run, create.Count, add.Count), Views(run.Ranked), create, add, outcome?.Skipped ?? [], run.Warnings, null, []);
    }

    public async Task<RetroAnalysisOutcome> ApplyAsync(Guid projectId, RecordRetroAnalysisRequest request, RetroActor actor, CancellationToken cancellationToken)
    {
        var run = await PrepareAsync(projectId, request, strict: false, cancellationToken);
        var applied = run.Plan.Actions.Count == 0 ? null : await planner.ApplyWithResultsAsync(projectId, run.Plan, actor, cancellationToken);
        var created = applied?.Outcome.Created ?? [];
        var actionByPattern = await ActionIdsAsync(projectId, run, applied?.Actions ?? [], cancellationToken);

        var newActions = applied?.Actions.Count(action => action.Created) ?? 0;
        var addedTo = applied?.Actions.Count(action => action.Key is not null && !action.Created) ?? 0;
        var funnel = Funnel(run, newActions, addedTo);

        Guid analysisId;
        try
        {
            analysisId = await store.StoreAsync(projectId, request, actor, run.Ranked, actionByPattern, funnel, cancellationToken);
        }
        catch (NpgsqlException exception) when (created.Count > 0)
        {
            logger.LogError(exception, "Storing the retro analysis failed after {Count} action items were created.", created.Count);
            throw ApiException.Conflict($"The analysis could not be stored. These action items were already created: {string.Join(", ", created)}. {exception.Message}");
        }

        return new RetroAnalysisOutcome(
            funnel, Views(run.Ranked), applied?.Outcome.WouldCreate ?? [], applied?.Outcome.WouldAdd ?? [], applied?.Outcome.Skipped ?? [], run.Warnings, analysisId, created);
    }

    private async Task<Run> PrepareAsync(Guid projectId, RecordRetroAnalysisRequest request, bool strict, CancellationToken cancellationToken)
    {
        var all = await scopeReader.PointsAsync(projectId, request.Scope, cancellationToken);
        var candidates = all.Values.Where(point => point.Category == RetroCategory.WentWell || point.Undecided).Select(point => point.Id).ToList();
        var covered = request.ExcludeCovered && candidates.Count > 0 ? await reads.CoveredAsync(projectId, candidates, cancellationToken) : [];
        var inScope = candidates.Where(id => !covered.Contains(id)).ToHashSet();

        await ValidateAsync(projectId, request, all, strict ? inScope : null, cancellationToken);

        var evidence = new Dictionary<string, RetroEvidence>(StringComparer.Ordinal);
        foreach (var pattern in request.Patterns)
        {
            evidence[pattern.Ref] = await reads.EvidenceAsync(pattern.PointIds, cancellationToken);
        }

        var ranked = RetroAnalysisRanking.RankPatterns(request, evidence);
        var warnings = new List<string>();
        var sent = request.Points.Select(point => point.Id).ToHashSet();
        var missing = candidates.Where(id => inScope.Contains(id) && !sent.Contains(id)).ToList();
        if (missing.Count > 0)
        {
            var listed = string.Join(", ", missing.Take(MaxListedPoints));
            warnings.Add($"{missing.Count} in-scope points were not in the run: {listed}{(missing.Count > MaxListedPoints ? ", and more" : string.Empty)}.");
        }

        var actions = new List<DecidePlanAction>();
        var refs = new List<string>();
        foreach (var entry in ranked.Where(entry => entry.Pattern.Kind == RetroPatternKind.Problem && entry.Action is not null))
        {
            var points = entry.Pattern.PointIds.Where(id => all[id].Category != RetroCategory.WentWell).ToList();
            if (points.Count == 0)
            {
                warnings.Add($"Pattern \"{entry.Pattern.Ref}\" has no points that need a decision, so it makes no action item.");
                continue;
            }

            var top = entry.Action!.Fix;
            actions.Add(new DecidePlanAction(top.Title, top.FixType, points, null, top.Existing));
            refs.Add(entry.Pattern.Ref);
        }

        return new Run(request, inScope.Count, ranked, warnings, new DecidePlan(request.Scope, actions, [], [], []), refs);
    }

    // The checks that need the database. A preview wants every point in scope and undecided (or went-well); an apply only wants them to exist in the scope, because a point
    // decided since the preview is skipped by the planner, not refused.
    private async Task ValidateAsync(
        Guid projectId, RecordRetroAnalysisRequest request, IReadOnlyDictionary<Guid, DecideScopePoint> all, HashSet<Guid>? mustBeInScope, CancellationToken cancellationToken)
    {
        for (var index = 0; index < request.Points.Count; index++)
        {
            var point = request.Points[index];
            if (!all.ContainsKey(point.Id) || (mustBeInScope is not null && !mustBeInScope.Contains(point.Id)))
            {
                throw ApiException.Validation($"Point {point.Id} is not in the scope you gave, or it is already decided or covered by an earlier analysis.", $"points[{index}].id");
            }

            if (point.FoldedInto is { } target && !all.ContainsKey(target))
            {
                throw ApiException.Validation($"Point {target} is not in the scope you gave.", $"points[{index}].folded_into");
            }
        }

        var continues = request.Patterns.Select(pattern => pattern.Continues).OfType<Guid>().Distinct().ToList();
        if (continues.Count == 0)
        {
            return;
        }

        var found = await reads.ContinuablePatternsAsync(projectId, request.EmbeddingModel, continues, cancellationToken);
        for (var index = 0; index < request.Patterns.Count; index++)
        {
            if (request.Patterns[index].Continues is { } earlier && !found.Contains(earlier))
            {
                throw ApiException.Validation($"{earlier} is not a pattern of this project made with the embedding model {request.EmbeddingModel}.", $"patterns[{index}].continues");
            }
        }
    }

    // The work item id of each pattern's action item: the key the planner created or added to, looked up once.
    private async Task<IReadOnlyDictionary<string, Guid>> ActionIdsAsync(Guid projectId, Run run, IReadOnlyList<DecideActionResult> results, CancellationToken cancellationToken)
    {
        var keys = results.Select(result => result.Key).OfType<string>().Distinct().ToList();
        if (keys.Count == 0)
        {
            return new Dictionary<string, Guid>();
        }

        var ids = await reads.WorkItemIdsAsync(projectId, keys, cancellationToken);
        var byPattern = new Dictionary<string, Guid>(StringComparer.Ordinal);
        for (var index = 0; index < results.Count; index++)
        {
            if (results[index].Key is { } key && ids.TryGetValue(key, out var id))
            {
                byPattern[run.ActionPatterns[index]] = id;
            }
        }

        return byPattern;
    }

    private static List<RetroRankedPattern> Views(IReadOnlyList<RetroAnalysisRanking.RankedPattern> ranked)
        => [.. ranked.Select(entry => new RetroRankedPattern(entry.Pattern.Ref, entry.Pattern.Kind, entry.Pattern.Title, entry.Rank, entry.Pattern.Continues, entry.Evidence))];

    private static RetroFunnel Funnel(Run run, int actionsNew, int actionsAdded)
    {
        var points = run.Request.Points;
        int Count(RetroPointOutcome outcome) => points.Count(point => point.Outcome == outcome);
        var clustered = Count(RetroPointOutcome.Clustered);
        var unclustered = Count(RetroPointOutcome.Unclustered);
        var generic = clustered + unclustered;
        var fixes = run.Request.Fixes;
        return new RetroFunnel(
            run.PointsIn, Count(RetroPointOutcome.Noise), Count(RetroPointOutcome.Folded), Count(RetroPointOutcome.OneOff), generic, clustered,
            generic == 0 ? 0 : Math.Round(clustered * 100.0 / generic, 1), unclustered,
            run.Request.Patterns.Count(pattern => pattern.Kind == RetroPatternKind.Problem), run.Request.Patterns.Count(pattern => pattern.Kind == RetroPatternKind.WentWell),
            fixes.Count, fixes.Count(fix => fix.Enforcement == RetroEnforcement.Fully), fixes.Count(fix => fix.Enforcement == RetroEnforcement.Partly),
            fixes.Count(fix => fix.Enforcement == RetroEnforcement.Discarded), actionsNew, actionsAdded);
    }

    // ActionPatterns[i] is the ref of the pattern whose action is Plan.Actions[i].
    private sealed record Run(
        RecordRetroAnalysisRequest Request, int PointsIn, IReadOnlyList<RetroAnalysisRanking.RankedPattern> Ranked, List<string> Warnings, DecidePlan Plan, IReadOnlyList<string> ActionPatterns);
}
