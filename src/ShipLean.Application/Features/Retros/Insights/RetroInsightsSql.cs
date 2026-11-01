using ShipLean.Application.Identity;

namespace ShipLean.Application.Features.Retros.Insights;

internal static class RetroInsightsSql
{
    public const string Sprints = """
        SELECT sp.id, sp.name, to_char(sp.start_date, 'YYYY-MM-DD') AS startDate, to_char(sp.end_date, 'YYYY-MM-DD') AS endDate
        FROM sprint sp
        WHERE sp.project_id = @projectId AND sp.archived_at IS NULL
        ORDER BY sp.start_date, sp.name, sp.id
        """;

    public const string Themes = """
        SELECT t.id, t.name, t.tone
        FROM retro_theme t
        WHERE t.project_id = @projectId AND t.archived_at IS NULL AND t.merged_into_id IS NULL
        """;

    // Live points of live retros that sit in a sprint and a theme: retrospective.sprint_id is the only sprint attribution.
    public const string Points = """
        SELECT p.id, p.retrospective_id AS retroId, p.theme_id AS themeId, r.sprint_id AS sprintId, p.category, p.created_at AS createdAt
        FROM retro_point p
        JOIN retrospective r ON r.id = p.retrospective_id
        WHERE r.project_id = @projectId AND r.archived_at IS NULL AND p.archived_at IS NULL
          AND p.theme_id IS NOT NULL AND r.sprint_id IS NOT NULL
        """;

    public static readonly string Actions = $"""
        SELECT ra.work_item_id AS workItemId, ra.theme_id AS themeId, ra.raised_sprint_id AS raisedSprintId,
               w.key, w.title, w.story_points::numeric AS points, w.created_at AS createdAt,
               st.id AS stateId, st.name AS stateName, st.category AS stateCategory, w.sprint_id AS sprintId,
               u.id AS ownerId, u.name AS ownerName, u.initials AS ownerInitials, u.avatar_colour AS ownerColour, {UserRefs.PhotoUrlSql} AS ownerPhotoUrl
        FROM retro_action ra
        JOIN work_item w ON w.id = ra.work_item_id
        JOIN state st ON st.id = w.state_id
        LEFT JOIN app_user u ON u.id = w.assigned_to
        WHERE ra.project_id = @projectId
        """;
}
