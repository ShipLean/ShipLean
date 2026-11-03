using ShipLean.Application.Features.Retros.Reads;

namespace ShipLean.Application.Features.Retros.Search;

internal static class RetroSearchSql
{
    public const string Sprints = """
        SELECT id, name AS sprintName, to_char(start_date, 'YYYY-MM-DD') AS startDate, to_char(end_date, 'YYYY-MM-DD') AS endDate
        FROM sprint
        WHERE project_id = @projectId AND archived_at IS NULL
        ORDER BY start_date, end_date, id
        """;

    public const string Themes = """
        SELECT id, name AS themeName, tone
        FROM retro_theme
        WHERE project_id = @projectId AND archived_at IS NULL AND merged_into_id IS NULL AND point_count > 0
        ORDER BY lower(name), id
        """;

    // Live points of live retros that sit in a sprint and a theme: retrospective.sprint_id is the only sprint attribution.
    public const string Mentions = """
        SELECT p.theme_id AS themeId, r.sprint_id AS sprintId, p.category
        FROM retro_point p
        JOIN retrospective r ON r.id = p.retrospective_id
        WHERE r.project_id = @projectId AND r.archived_at IS NULL AND p.archived_at IS NULL
          AND p.theme_id IS NOT NULL AND r.sprint_id IS NOT NULL
        """;

    // The closed action items on themes; a removed one is not closed.
    public const string ClosedActions = """
        SELECT ra.work_item_id AS itemId, ra.theme_id AS themeId
        FROM retro_action ra
        JOIN work_item w ON w.id = ra.work_item_id
        JOIN state st ON st.id = w.state_id
        WHERE ra.project_id = @projectId AND ra.theme_id IS NOT NULL AND st.category = 'completed'
        """;

    public const string LiveRetro = "SELECT id FROM retrospective WHERE id = @selected AND project_id = @projectId AND archived_at IS NULL";

    // Needs @projectId, @storyLevel, @hasQuery, @query, @pattern, @sprint, @category, @theme, @level, @role, @onlyThemes (null for no filter), @noSprint, @decision (undecided, actioned, notNeeded or null), @unthemed.
    // Rank tiers: a point's own text or root cause (2 plus ts_rank), then theme name, key or title (1), then the retro's failure log
    // (below 1), which counts for the retro's first live point only and only when no live point of that retro matches on text or theme. Without a query the newest point comes first.
    public static readonly string Rows = $$"""
        WITH q AS (
            SELECT CASE WHEN @hasQuery THEN websearch_to_tsquery('english', @query) || websearch_to_tsquery('simple', @query) END AS tsq
        ),
        candidate AS (
            SELECT p.id, p.retrospective_id AS retroId, p.theme_id AS themeId, p.created_at AS createdAt, p.position,
                   CASE WHEN @hasQuery THEN greatest(
                       CASE WHEN p.tsv @@ q.tsq THEN 2 + ts_rank(p.tsv, q.tsq) ELSE 0 END,
                       CASE WHEN (th.archived_at IS NULL AND th.name ILIKE @pattern) OR w.key ILIKE @pattern OR w.title ILIKE @pattern THEN 1 ELSE 0 END,
                       CASE WHEN p.id = (SELECT f.id FROM retro_point f WHERE f.retrospective_id = r.id AND f.archived_at IS NULL ORDER BY f.position, f.created_at, f.id LIMIT 1)
                             AND NOT EXISTS (SELECT 1 FROM retro_point m LEFT JOIN retro_theme mt ON mt.id = m.theme_id
                                             WHERE m.retrospective_id = r.id AND m.archived_at IS NULL
                                               AND (m.tsv @@ q.tsq OR (mt.archived_at IS NULL AND mt.name ILIKE @pattern)))
                            THEN coalesce((SELECT 0.1 + 0.5 * max(ts_rank(l.tsv, q.tsq)) FROM retro_failure_log l WHERE l.retrospective_id = r.id AND l.tsv @@ q.tsq), 0)
                            ELSE 0 END)
                   ELSE 0 END AS rank
            FROM q, retro_point p
            JOIN retrospective r ON r.id = p.retrospective_id
            LEFT JOIN retro_theme th ON th.id = p.theme_id
            LEFT JOIN work_item w ON w.id = r.work_item_id
            WHERE r.project_id = @projectId AND r.archived_at IS NULL AND p.archived_at IS NULL
              AND (@sprint::uuid IS NULL OR r.sprint_id = @sprint)
              AND (@category::text IS NULL OR p.category = @category)
              AND (@theme::uuid IS NULL OR p.theme_id = @theme)
              AND (@role::text IS NULL OR r.role = @role)
              AND (@level::text IS NULL
                   OR (@level = 'stories' AND w.type = ANY(@storyLevel))
                   OR (@level = 'features' AND w.type = 'feature'))
              AND (@onlyThemes::uuid[] IS NULL OR p.theme_id = ANY(@onlyThemes))
              AND (NOT @noSprint OR r.sprint_id IS NULL)
              AND (NOT @unthemed OR p.theme_id IS NULL)
              AND (@decision::text IS NULL OR {{RetroDecisionSql.Decision("p")}} = @decision)
        )
        SELECT id, retroId, themeId, @hasQuery AND rank < 1 AS logOnly, count(*) OVER ()::int AS total
        FROM candidate
        WHERE NOT @hasQuery OR rank > 0
        ORDER BY rank DESC, createdAt DESC, position, id
        LIMIT 200
        """;
}
