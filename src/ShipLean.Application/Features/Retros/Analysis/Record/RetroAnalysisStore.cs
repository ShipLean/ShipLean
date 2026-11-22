using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Retros.Analysis;
using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.Application.Features.Retros.Analysis.Record;

// Stores one analysis in one transaction: the analysis, its patterns, the points it covered and the fixes with the action item each pattern's acting fixes share.
internal sealed class RetroAnalysisStore(NpgsqlDataSource dataSource)
{
    public async Task<Guid> StoreAsync(
        Guid projectId, RecordRetroAnalysisRequest request, RetroActor actor, IReadOnlyList<RetroAnalysisRanking.RankedPattern> ranked,
        IReadOnlyDictionary<string, Guid> actionByPattern, RetroFunnel funnel, CancellationToken cancellationToken)
    {
        var analysisId = Guid.NewGuid();
        var patternIds = ranked.ToDictionary(entry => entry.Pattern.Ref, _ => Guid.NewGuid(), StringComparer.Ordinal);
        var patternOfPoint = ranked.SelectMany(entry => entry.Pattern.PointIds.Select(id => (id, pattern: patternIds[entry.Pattern.Ref]))).ToDictionary(pair => pair.id, pair => pair.pattern);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO retro_analysis (id, project_id, scope_kind, feature_id, sprint_id, range_from, range_to, created_by_user_id, agent_name, embedding_model, rewrite_model, report_markdown, points_in, actions_new, actions_added)
                VALUES (@analysisId, @projectId, @scopeKind, @featureId, @sprintId, @rangeFrom::date, @rangeTo::date, @userId, @agentName, @embeddingModel, @rewriteModel, @report, @pointsIn, @actionsNew, @actionsAdded)
                """,
                new
                {
                    analysisId,
                    projectId,
                    scopeKind = ScopeKind(request.Scope),
                    featureId = request.Scope.FeatureId,
                    sprintId = request.Scope.SprintId,
                    rangeFrom = request.Scope.From?.ToDateTime(TimeOnly.MinValue),
                    rangeTo = request.Scope.To?.ToDateTime(TimeOnly.MinValue),
                    userId = actor.UserId,
                    agentName = actor.AgentName ?? "unknown",
                    embeddingModel = request.EmbeddingModel,
                    rewriteModel = request.RewriteModel,
                    report = request.ReportMarkdown,
                    pointsIn = funnel.PointsIn,
                    actionsNew = funnel.ActionsNew,
                    actionsAdded = funnel.ActionsAdded,
                },
                transaction,
                cancellationToken: cancellationToken));

        foreach (var entry in ranked)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO retro_pattern (id, analysis_id, ref, kind, title, lesson, asks, centroid, continues_pattern_id, rank)
                    VALUES (@id, @analysisId, @ref, @kind, @title, @lesson, @asks, @centroid, @continues, @rank)
                    """,
                    new
                    {
                        id = patternIds[entry.Pattern.Ref],
                        analysisId,
                        @ref = entry.Pattern.Ref,
                        kind = Camel(entry.Pattern.Kind),
                        title = entry.Pattern.Title,
                        lesson = entry.Pattern.Lesson,
                        asks = entry.Pattern.Asks.ToArray(),
                        centroid = entry.Pattern.Centroid?.ToArray(),
                        continues = entry.Pattern.Continues,
                        rank = entry.Rank,
                    },
                    transaction,
                    cancellationToken: cancellationToken));
        }

        foreach (var point in request.Points)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO retro_analysis_point (analysis_id, point_id, outcome, folded_into_point_id, generic_text, pattern_id)
                    VALUES (@analysisId, @pointId, @outcome, @foldedInto, @generic, @patternId)
                    """,
                    new
                    {
                        analysisId,
                        pointId = point.Id,
                        outcome = Camel(point.Outcome),
                        foldedInto = point.FoldedInto,
                        generic = point.Generic,
                        patternId = patternOfPoint.TryGetValue(point.Id, out var pattern) ? pattern : (Guid?)null,
                    },
                    transaction,
                    cancellationToken: cancellationToken));
        }

        foreach (var entry in ranked)
        {
            foreach (var ranking in entry.Fixes)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO retro_pattern_fix (id, pattern_id, title, fix_type, enforcement, enforced_by, reason, action_work_item_id, rank)
                        VALUES (@id, @patternId, @title, @fixType, @enforcement, @enforcedBy, @reason, @actionId, @rank)
                        """,
                        new
                        {
                            id = Guid.NewGuid(),
                            patternId = patternIds[entry.Pattern.Ref],
                            title = ranking.Fix.Title,
                            fixType = Camel(ranking.Fix.FixType),
                            enforcement = Camel(ranking.Fix.Enforcement),
                            enforcedBy = ranking.Fix.EnforcedBy,
                            reason = ranking.Fix.Reason,
                            actionId = ranking.Acting && actionByPattern.TryGetValue(entry.Pattern.Ref, out var action) ? action : (Guid?)null,
                            rank = ranking.Rank,
                        },
                        transaction,
                        cancellationToken: cancellationToken));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return analysisId;
    }

    private static string ScopeKind(DecideScope scope)
    {
        if (scope.FeatureId is not null)
        {
            return "feature";
        }

        return scope.SprintId is not null ? "sprint" : "range";
    }

    private static string Camel<T>(T value)
        where T : struct, Enum
        => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
