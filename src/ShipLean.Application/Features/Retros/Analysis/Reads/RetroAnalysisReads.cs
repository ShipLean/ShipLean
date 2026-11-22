using Dapper;
using Npgsql;
using ShipLean.Contracts.Retros.Analysis;

namespace ShipLean.Application.Features.Retros.Analysis.Reads;

// get_retro_analysis (C3) over the stored analysis tables. Evidence is recomputed over each pattern's points; the action's state is read live.
internal sealed class RetroAnalysisReads(NpgsqlDataSource dataSource) : IRetroAnalysisReads
{
    // The order fixes_by_tier lists tiers in: most deterministic first (D2).
    private static readonly string[] Tiers = ["fix", "structure", "analyzer", "test", "skill", "guide", "product", "other"];

    public async Task<RetroAnalysisView?> GetAsync(Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var args = new { id, projectId };
        var analysis = await connection.QuerySingleOrDefaultAsync<AnalysisRow>(new CommandDefinition(RetroAnalysisReadSql.Analysis, args, cancellationToken: cancellationToken));
        if (analysis is null)
        {
            return null;
        }

        var outcomes = await connection.QuerySingleAsync<OutcomeRow>(new CommandDefinition(RetroAnalysisReadSql.Outcomes, args, cancellationToken: cancellationToken));
        var actions = await connection.QuerySingleAsync<ActionRow>(new CommandDefinition(RetroAnalysisReadSql.Actions, args, cancellationToken: cancellationToken));
        var patternRows = (await connection.QueryAsync<PatternRow>(new CommandDefinition(RetroAnalysisReadSql.Patterns, args, cancellationToken: cancellationToken))).ToList();
        var fixRows = (await connection.QueryAsync<FixRow>(new CommandDefinition(RetroAnalysisReadSql.Fixes, args, cancellationToken: cancellationToken))).ToList();

        var live = fixRows.Where(row => row.Enforcement != RetroEnforcement.Discarded).ToList();
        var byPattern = live.ToLookup(row => row.PatternId);
        var patterns = patternRows.Select(row => Pattern(row, byPattern[row.Id].Select(View).ToList())).ToList();
        var fixesByTier = new Dictionary<string, IReadOnlyList<RetroViewFix>>();
        foreach (var tier in Tiers)
        {
            var inTier = live.Where(row => row.FixType == tier).Select(View).ToList();
            if (inTier.Count > 0)
            {
                fixesByTier[tier] = inTier;
            }
        }

        return new RetroAnalysisView(
            analysis.Id, Scope(analysis.ScopeKind, analysis.Feature, analysis.Sprint, analysis.RangeFrom, analysis.RangeTo), Utc(analysis.CreatedAt), analysis.AgentName,
            analysis.EmbeddingModel, analysis.RewriteModel, Funnel(outcomes, actions, patterns, fixRows), [.. patterns.Where(pattern => pattern.Kind == RetroPatternKind.Problem)],
            [.. patterns.Where(pattern => pattern.Kind == RetroPatternKind.WentWell)], fixesByTier, [.. fixRows.Where(row => row.Enforcement == RetroEnforcement.Discarded).Select(View)],
            analysis.ReportMarkdown);
    }

    public async Task<IReadOnlyList<RetroAnalysisRow>> ListAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ListRow>(new CommandDefinition(RetroAnalysisReadSql.Newest, new { projectId }, cancellationToken: cancellationToken));
        return [.. rows.Select(row => new RetroAnalysisRow(row.Id, Scope(row.ScopeKind, row.Feature, row.Sprint, row.RangeFrom, row.RangeTo), Utc(row.CreatedAt), row.AgentName, row.Patterns, row.ActionsNew))];
    }

    // Funnel numbers come from the stored rows, so they can't drift from the data. PointsIn is the points the analysis recorded an outcome for: the in-scope count at the time of the run is not stored.
    private static RetroFunnel Funnel(OutcomeRow outcomes, ActionRow actions, List<RetroViewPattern> patterns, List<FixRow> fixes)
    {
        var generic = outcomes.Clustered + outcomes.Unclustered;
        return new RetroFunnel(
            outcomes.PointsIn, outcomes.Noise, outcomes.Folded, outcomes.OneOff, generic, outcomes.Clustered, generic == 0 ? 0 : Math.Round(outcomes.Clustered * 100.0 / generic, 1), outcomes.Unclustered,
            patterns.Count(pattern => pattern.Kind == RetroPatternKind.Problem), patterns.Count(pattern => pattern.Kind == RetroPatternKind.WentWell), fixes.Count,
            fixes.Count(row => row.Enforcement == RetroEnforcement.Fully), fixes.Count(row => row.Enforcement == RetroEnforcement.Partly),
            fixes.Count(row => row.Enforcement == RetroEnforcement.Discarded), actions.ActionsNew, actions.ActionsAdded);
    }

    private static RetroViewPattern Pattern(PatternRow row, List<RetroViewFix> fixes)
        => new(
            row.Id, row.Ref, row.Kind == "wentWell" ? RetroPatternKind.WentWell : RetroPatternKind.Problem, row.Title, row.Lesson, row.Rank, row.Continues,
            new RetroEvidence(
                row.Points, [.. row.Items.Cast<string>()], [.. row.Features.Cast<string>()], [.. row.Sprints.Cast<string>()], [.. row.Agents.Cast<string>()],
                row.FirstLogged is { } first ? Utc(first) : null, row.LastLogged is { } last ? Utc(last) : null),
            fixes);

    private static RetroViewFix View(FixRow row)
        => new(row.Title, row.FixType, row.Enforcement, row.EnforcedBy, row.Reason, row.ActionKey is null ? null : new RetroViewAction(row.ActionKey, row.ActionState ?? string.Empty));

    private static RetroScopeView Scope(string kind, string? feature, string? sprint, DateTime? from, DateTime? to)
        => new(
            kind switch { "feature" => RetroScopeKind.Feature, "sprint" => RetroScopeKind.Sprint, _ => RetroScopeKind.Range },
            feature, sprint, from is { } f ? DateOnly.FromDateTime(f) : null, to is { } t ? DateOnly.FromDateTime(t) : null);

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record AnalysisRow(
        Guid Id, string ScopeKind, string? Feature, string? Sprint, DateTime? RangeFrom, DateTime? RangeTo, DateTime CreatedAt, string AgentName, string EmbeddingModel, string? RewriteModel,
        string ReportMarkdown);

    private sealed record ListRow(Guid Id, string ScopeKind, string? Feature, string? Sprint, DateTime? RangeFrom, DateTime? RangeTo, DateTime CreatedAt, string AgentName, int Patterns, int ActionsNew);

    private sealed record OutcomeRow(int PointsIn, int Noise, int Folded, int OneOff, int Clustered, int Unclustered);

    private sealed record ActionRow(int ActionsNew, int ActionsAdded);

    // Array, not string[]: Dapper's constructor matching sees Postgres text[] as System.Array.
    private sealed record PatternRow(
        Guid Id, string Ref, string Kind, string Title, string Lesson, int Rank, Guid? Continues, int Points, Array Items, Array Features, Array Sprints, Array Agents, DateTime? FirstLogged,
        DateTime? LastLogged);

    private sealed record FixRow(Guid PatternId, string Title, string FixType, string EnforcementText, string? EnforcedBy, string Reason, string? ActionKey, string? ActionState)
    {
        public RetroEnforcement Enforcement => EnforcementText switch
        {
            "partly" => RetroEnforcement.Partly,
            "fully" => RetroEnforcement.Fully,
            "discarded" => RetroEnforcement.Discarded,
            _ => RetroEnforcement.NotCaught,
        };
    }
}
