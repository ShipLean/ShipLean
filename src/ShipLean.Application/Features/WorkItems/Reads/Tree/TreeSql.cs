using ShipLean.Application.Identity;

namespace ShipLean.Application.Features.WorkItems.Reads.Tree;

// The definitions every tree read shares, in one place. Alias `w` is the work_item and `s` its state.
internal static class TreeSql
{
    // Closed = the Completed category; open = every other live state; Removed is never live.
    public const string Live = "s.category <> 'removed'";

    public const string Closed = "s.category = 'completed'";

    public const string Open = "s.category NOT IN ('completed', 'removed')";

    // Stack order: what WorkItemOrdering writes (backlog_order, then id).
    public const string StackOrder = "w.backlog_order, w.id";

    // Every descendant of the @roots, at any depth, with the root it hangs under and its depth below that root (1 = direct
    // child). Removed items are walked through (their children are still live work) and filtered out by Live where the rows
    // are read. An item has one parent, so a cycle can only come back through its own root: the recursive step never lists a
    // root among its own descendants, which ends the walk.
    public const string DescendantsCte =
        """
        WITH RECURSIVE tree (id, root_id, depth) AS (
            SELECT c.id, c.parent_id, 1 FROM work_item c WHERE c.project_id = @projectId AND c.parent_id = ANY(@roots)
            UNION ALL
            SELECT c.id, t.root_id, t.depth + 1 FROM tree t JOIN work_item c ON c.parent_id = t.id AND c.project_id = @projectId
            WHERE c.id <> t.root_id
        )
        """;

    // The columns every item read maps onto ItemRow.
    public const string ItemColumns =
        $"""
        w.id, w.key, w.title, w.type, w.story_points AS points, w.changed_at AS changedAt,
        jsonb_array_length(w.acceptance_criteria) > 0 AS hasAcceptanceCriteria,
        s.id AS stateId, s.name AS stateName, s.category AS stateCategory,
        sp.id AS sprintId, sp.name AS sprintName, to_char(sp.start_date, 'YYYY-MM-DD') AS sprintStart, to_char(sp.end_date, 'YYYY-MM-DD') AS sprintEnd,
        u.id AS assigneeId, u.name AS assigneeName, u.initials AS assigneeInitials, u.avatar_colour AS assigneeColour,
        {UserRefs.PhotoUrlSql} AS assigneePhotoUrl,
        p.id AS parentId, p.key AS parentKey, p.type AS parentType, p.title AS parentTitle,
        w.tags AS tags, w.revision AS revision
        """;

    public const string ItemJoins =
        """
        JOIN state s ON s.id = w.state_id
        LEFT JOIN sprint sp ON sp.id = w.sprint_id
        LEFT JOIN app_user u ON u.id = w.assigned_to
        LEFT JOIN work_item p ON p.id = w.parent_id AND p.project_id = w.project_id
        """;
}
