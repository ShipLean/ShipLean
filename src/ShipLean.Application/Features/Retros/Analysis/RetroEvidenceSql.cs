namespace ShipLean.Application.Features.Retros.Analysis;

// The evidence rule of C2 and C3 as SQL: over a set of point ids, how many points and which items, Features, sprints and agents they span, and the first and last time
// one was logged. A point's Feature is its item when that is a Feature, else the item's parent or grandparent Feature (a task sits under a story); its sprint is its retro's
// sprint, else its item's; its agent is the point's, else its retro's. Archived points count: the evidence is of what the analysis covered when it ran.
// One row: points, items, features, sprints, agents (text[]), first_logged, last_logged (timestamptz).
public static class RetroEvidenceSql
{
    public static string ForPoints(string pointIdsParameter = "@pointIds")
        => $"""
            SELECT count(*)::int AS points,
                   coalesce(array_agg(DISTINCT w.key ORDER BY w.key) FILTER (WHERE w.key IS NOT NULL), ARRAY[]::text[]) AS items,
                   coalesce(array_agg(DISTINCT f.key ORDER BY f.key) FILTER (WHERE f.key IS NOT NULL), ARRAY[]::text[]) AS features,
                   coalesce(array_agg(DISTINCT s.name ORDER BY s.name) FILTER (WHERE s.name IS NOT NULL), ARRAY[]::text[]) AS sprints,
                   coalesce(array_agg(DISTINCT coalesce(p.agent_name, r.agent_name) ORDER BY coalesce(p.agent_name, r.agent_name)) FILTER (WHERE coalesce(p.agent_name, r.agent_name) IS NOT NULL), ARRAY[]::text[]) AS agents,
                   min(p.created_at) AS first_logged,
                   max(p.created_at) AS last_logged
            FROM retro_point p
            JOIN retrospective r ON r.id = p.retrospective_id
            LEFT JOIN work_item w ON w.id = r.work_item_id
            LEFT JOIN work_item par ON par.id = w.parent_id
            LEFT JOIN work_item gp ON gp.id = par.parent_id
            LEFT JOIN work_item f ON f.id = CASE WHEN w.type = 'feature' THEN w.id WHEN par.type = 'feature' THEN par.id WHEN gp.type = 'feature' THEN gp.id END
            LEFT JOIN sprint s ON s.id = coalesce(r.sprint_id, w.sprint_id)
            WHERE p.id = ANY({pointIdsParameter})
            """;
}
