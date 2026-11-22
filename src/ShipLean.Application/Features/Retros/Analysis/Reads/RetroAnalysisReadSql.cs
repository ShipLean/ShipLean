namespace ShipLean.Application.Features.Retros.Analysis.Reads;

// The SQL of get_retro_analysis (C3), one statement per concern. Evidence is the shared RetroEvidenceSql over each pattern's stored points.
internal static class RetroAnalysisReadSql
{
    private const string PointsOfPattern = "coalesce((SELECT array_agg(rp.point_id) FROM retro_analysis_point rp WHERE rp.pattern_id = pt.id), ARRAY[]::uuid[])";

    // An action item counts as new when its work item was made in the apply that stored the analysis, which creates the actions just before the analysis rows: within an hour before it.
    private const string MadeByThisApply = "w.created_at >= a.created_at - interval '1 hour'";

    public const string Analysis =
        """
        SELECT a.id, a.scope_kind AS scopeKind, f.key AS feature, s.name AS sprint, a.range_from::timestamp AS rangeFrom, a.range_to::timestamp AS rangeTo,
               a.created_at AS createdAt, a.agent_name AS agentName, a.embedding_model AS embeddingModel, a.rewrite_model AS rewriteModel, a.report_markdown AS reportMarkdown
        FROM retro_analysis a
        LEFT JOIN work_item f ON f.id = a.feature_id
        LEFT JOIN sprint s ON s.id = a.sprint_id
        WHERE a.id = @id AND a.project_id = @projectId AND a.archived_at IS NULL
        """;

    public const string Outcomes =
        """
        SELECT count(*)::int AS pointsIn,
               count(*) FILTER (WHERE outcome = 'noise')::int AS noise,
               count(*) FILTER (WHERE outcome = 'folded')::int AS folded,
               count(*) FILTER (WHERE outcome = 'oneOff')::int AS oneOff,
               count(*) FILTER (WHERE outcome = 'clustered')::int AS clustered,
               count(*) FILTER (WHERE outcome = 'unclustered')::int AS unclustered
        FROM retro_analysis_point
        WHERE analysis_id = @id
        """;

    public const string Actions =
        $"""
        SELECT count(*) FILTER (WHERE {MadeByThisApply})::int AS actionsNew,
               count(*) FILTER (WHERE NOT ({MadeByThisApply}))::int AS actionsAdded
        FROM retro_analysis a
        JOIN work_item w ON w.id IN (SELECT fx.action_work_item_id FROM retro_pattern_fix fx JOIN retro_pattern pt ON pt.id = fx.pattern_id WHERE pt.analysis_id = a.id)
        WHERE a.id = @id
        """;

    public static readonly string Patterns =
        $"""
        SELECT pt.id, pt.ref, pt.kind, pt.title, pt.lesson, pt.rank, pt.continues_pattern_id AS continues,
               e.points, e.items, e.features, e.sprints, e.agents, e.first_logged AS firstLogged, e.last_logged AS lastLogged
        FROM retro_pattern pt
        CROSS JOIN LATERAL ({RetroEvidenceSql.ForPoints(PointsOfPattern)}) e
        WHERE pt.analysis_id = @id
        ORDER BY pt.rank, pt.ref
        """;

    public const string Fixes =
        """
        SELECT fx.pattern_id AS patternId, fx.title, fx.fix_type AS fixType, fx.enforcement AS enforcementText, fx.enforced_by AS enforcedBy, fx.reason, w.key AS actionKey, st.name AS actionState
        FROM retro_pattern_fix fx
        JOIN retro_pattern pt ON pt.id = fx.pattern_id
        LEFT JOIN work_item w ON w.id = fx.action_work_item_id
        LEFT JOIN state st ON st.id = w.state_id
        WHERE pt.analysis_id = @id
        ORDER BY pt.rank, pt.ref, fx.rank
        """;

    public const string Newest =
        $"""
        SELECT a.id, a.scope_kind AS scopeKind, f.key AS feature, s.name AS sprint, a.range_from::timestamp AS rangeFrom, a.range_to::timestamp AS rangeTo,
               a.created_at AS createdAt, a.agent_name AS agentName,
               (SELECT count(*)::int FROM retro_pattern pt WHERE pt.analysis_id = a.id) AS patterns,
               (SELECT count(DISTINCT w.id)::int
                FROM retro_pattern_fix fx
                JOIN retro_pattern pt ON pt.id = fx.pattern_id
                JOIN work_item w ON w.id = fx.action_work_item_id
                WHERE pt.analysis_id = a.id AND {MadeByThisApply}) AS actionsNew
        FROM retro_analysis a
        LEFT JOIN work_item f ON f.id = a.feature_id
        LEFT JOIN sprint s ON s.id = a.sprint_id
        WHERE a.project_id = @projectId AND a.archived_at IS NULL
        ORDER BY a.created_at DESC, a.id
        LIMIT 10
        """;
}
