using ShipLean.Application.Features.Retros.Decide;
using ShipLean.Application.Features.Retros.Reads;

namespace ShipLean.Application.Features.Retros.Analysis.Export;

// The SQL of the export, one statement per concern of a page. The scope, decision and covered rules are the shared fragments, never copies.
internal static class RetroExportSql
{
    // The points a page may hold: live, in scope, still needing a decision (went-well ones have none and come unless left out), not covered by an earlier analysis unless asked.
    private static readonly string Filter = $"""
        {DecideScopeSql.Where}
          AND ((p.category = 'wentWell' AND @includeWentWell) OR (p.category <> 'wentWell' AND {RetroDecisionSql.Undecided("p")}))
          AND (NOT @excludeCovered OR NOT {RetroCoveredSql.Covered("p")})
        """;

    public static readonly string Total = $"SELECT count(*)::int FROM {DecideScopeSql.From} WHERE {Filter}";

    // The nearest Feature above the retro's item (the item itself on a Feature), up to two levels: task -> story -> Feature, story -> Feature.
    public static readonly string Points = $"""
        SELECT p.id, p.retrospective_id AS retroId, p.category, p.text, p.root_cause AS rootCause, t.name AS theme, p.created_at AS loggedAt, p.agent_name AS agentName, r.role,
               w.id AS itemId, w.key AS itemKey, w.type AS itemType, w.title AS itemTitle,
               CASE WHEN w.type = 'feature' THEN w.goal ELSE w.description END AS itemBrief,
               f.key AS featureKey, f.title AS featureTitle, f.goal AS featureGoal, COALESCE(rs.name, ws.name) AS sprint
        FROM {DecideScopeSql.From}
        LEFT JOIN work_item w1 ON w1.id = w.parent_id
        LEFT JOIN work_item w2 ON w2.id = w1.parent_id
        LEFT JOIN work_item f ON f.id = CASE WHEN w.type = 'feature' THEN w.id WHEN w1.type = 'feature' THEN w1.id WHEN w2.type = 'feature' THEN w2.id END
        LEFT JOIN retro_theme t ON t.id = p.theme_id
        LEFT JOIN sprint rs ON rs.id = r.sprint_id
        LEFT JOIN sprint ws ON ws.id = w.sprint_id
        WHERE {Filter}
          AND (@afterAt::timestamptz IS NULL OR (p.created_at, p.id) > (@afterAt, @afterId::uuid))
        ORDER BY p.created_at, p.id
        LIMIT @take
        """;

    public const string Retros = """
        SELECT r.id, w.key AS item, COALESCE(rs.name, ws.name) AS sprint, r.agent_name AS agentName, r.role
        FROM retrospective r
        LEFT JOIN work_item w ON w.id = r.work_item_id
        LEFT JOIN sprint rs ON rs.id = r.sprint_id
        LEFT JOIN sprint ws ON ws.id = w.sprint_id
        WHERE r.id = ANY(@retroIds)
        """;

    public const string FailureLogs = """
        SELECT retrospective_id AS retroId, kind, text FROM retro_failure_log
        WHERE retrospective_id = ANY(@retroIds)
        ORDER BY retrospective_id, position, created_at
        """;

    // The item's own commits and those of its Tasks and Bugs, newest 20 per item, at most 50 paths each.
    public const string Commits = """
        SELECT itemId, repository, sha, subject, paths FROM (
            SELECT i.id AS itemId, cr.host || '/' || cr.path AS repository, c.sha, c.subject, c.paths[1:50] AS paths,
                   row_number() OVER (PARTITION BY i.id ORDER BY c.authored_at DESC, c.sha) AS rn
            FROM unnest(@itemIds::uuid[]) AS i(id)
            JOIN work_item cw ON cw.id = i.id OR (cw.parent_id = i.id AND cw.type IN ('task', 'bug'))
            JOIN work_item_commit wic ON wic.work_item_id = cw.id AND wic.archived_at IS NULL
            JOIN code_repository cr ON cr.id = wic.repository_id
            JOIN code_commit c ON c.repository_id = wic.repository_id AND c.sha = wic.sha
        ) ranked
        WHERE rn <= 20
        ORDER BY itemId, rn
        """;

    public const string OpenActions = """
        SELECT w.key, w.title, st.name AS state, ra.fix_type AS fixType, t.name AS theme,
               (SELECT count(*)::int FROM retro_action_point rap WHERE rap.work_item_id = ra.work_item_id AND rap.archived_at IS NULL) AS pointCount
        FROM retro_action ra
        JOIN work_item w ON w.id = ra.work_item_id
        JOIN state st ON st.id = w.state_id
        LEFT JOIN retro_theme t ON t.id = ra.theme_id
        WHERE ra.project_id = @projectId AND st.category NOT IN ('completed', 'removed')
        ORDER BY w.created_at
        """;

    // Live analyses of the project only: the newest, or the one named; another project's id finds nothing.
    public const string PatternsAnalysis = """
        SELECT id, embedding_model AS embeddingModel FROM retro_analysis
        WHERE project_id = @projectId AND archived_at IS NULL AND (@analysisId::uuid IS NULL OR id = @analysisId)
        ORDER BY created_at DESC, id
        LIMIT 1
        """;

    public const string Patterns = """
        SELECT id, kind, title, lesson, centroid FROM retro_pattern WHERE analysis_id = @analysisId ORDER BY rank, ref
        """;

    public const string ScopeNames = """
        SELECT (SELECT key FROM work_item WHERE id = @featureId) AS feature, (SELECT name FROM sprint WHERE id = @sprintId) AS sprint
        """;
}
