namespace ShipLean.Application.Features.Demos.Data;

// The SQL behind the sprint's demo data (K7). Nothing here selects retro_failure_log, root_cause, a comment body, an author, an agent
// name or a retro role: those name who said something or carry raw failure material. Review rounds are counted by the comment's
// prefix only. Aliases: w work item, s state.
internal static class SprintDemoDataSql
{
    public const string SprintAndProject =
        """
        SELECT sp.name AS SprintName, sp.goal AS SprintGoal, to_char(sp.start_date, 'YYYY-MM-DD') AS StartDate, to_char(sp.end_date, 'YYYY-MM-DD') AS EndDate,
               (sp.rollover_status = 'decided') AS Closed,
               p.key AS ProjectKey, p.name AS ProjectName, p.repo_url AS RepoUrl, p.repo_base_branch AS BaseBranch, COALESCE(d.environment, '') AS Environment
        FROM sprint sp
        JOIN project p ON p.id = sp.project_id
        LEFT JOIN project_demo_settings d ON d.project_id = p.id
        WHERE sp.id = @sprintId
        """;

    public const string Features =
        """
        SELECT w.id AS Id, w.key AS Key, w.title AS Title, s.name AS State, COALESCE(w.goal, '') AS Goal, w.description AS Description
        FROM work_item w
        JOIN state s ON s.id = w.state_id
        WHERE w.id = ANY(@featureIds)
        ORDER BY w.number
        """;

    // The sprint's stories and bugs: the Feature they hang under (null when the parent is not a Feature), then by key. Actions and hidden types are not story or bug.
    public const string Stories =
        """
        SELECT w.id AS Id, CASE WHEN w.parent_type = 'feature' THEN w.parent_id END AS FeatureId, w.key AS Key, w.type AS Type, w.title AS Title, s.name AS State,
               w.story_points::numeric AS Points, left(w.description, @summaryLength + 1) AS Summary, w.acceptance_criteria::text AS Criteria
        FROM work_item w
        JOIN state s ON s.id = w.state_id AND s.category <> 'removed'
        WHERE w.sprint_id = @sprintId AND w.type = ANY(@types)
        ORDER BY w.number
        """;

    public const string Tasks =
        """
        SELECT w.parent_id AS StoryId, w.title AS Title, s.name AS State
        FROM work_item w
        JOIN state s ON s.id = w.state_id AND s.category <> 'removed'
        WHERE w.parent_id = ANY(@storyIds) AND w.type = 'task'
        ORDER BY w.backlog_order, w.id
        """;

    // The live members of each Feature's plan: its stories, and their tasks.
    private const string PlanMembers =
        """
        WITH plan_members AS (
            SELECT w.id AS member_id, w.parent_id AS feature_id, 'story' AS kind, w.tags AS tags
            FROM work_item w
            JOIN state s ON s.id = w.state_id AND s.category <> 'removed'
            WHERE w.parent_id = ANY(@featureIds) AND w.type = 'story'
            UNION ALL
            SELECT t.id, st.parent_id, 'task', t.tags
            FROM work_item t
            JOIN state ts ON ts.id = t.state_id AND ts.category <> 'removed'
            JOIN work_item st ON st.id = t.parent_id AND st.type = 'story' AND st.parent_id = ANY(@featureIds)
            JOIN state ss ON ss.id = st.state_id AND ss.category <> 'removed'
            WHERE t.type = 'task'
        )
        """;

    public const string PlanShape = PlanMembers + " SELECT feature_id AS FeatureId, kind AS Kind, tags AS Tags FROM plan_members";

    public const string PlanReviews =
        PlanMembers +
        """
         SELECT m.feature_id AS FeatureId, count(*)::int AS Rounds
        FROM comment c
        JOIN plan_members m ON m.member_id = c.work_item_id AND m.kind = 'task'
        WHERE c.archived_at IS NULL AND c.body LIKE @reviewPrefix
        GROUP BY m.feature_id
        """;

    // Pull request links sit on the tasks that report them; the Feature's are the distinct addresses under it.
    public const string PullRequests =
        PlanMembers +
        """
         , members AS (
            SELECT member_id, feature_id FROM plan_members
            UNION ALL
            SELECT id, id FROM work_item WHERE id = ANY(@featureIds)
        )
        SELECT m.feature_id AS FeatureId, l.url AS Url
        FROM work_item_link l
        JOIN members m ON m.member_id = l.source_id
        WHERE l.link_type = 'url' AND l.archived_at IS NULL AND l.title LIKE @pullRequestPrefix
        ORDER BY l.created_at, l.id
        """;

    public const string Attachments =
        """
        SELECT work_item_id AS FeatureId, name AS Name
        FROM attachment
        WHERE work_item_id = ANY(@featureIds) AND archived_at IS NULL
        ORDER BY created_at, name, id
        """;

    // The sprint's live retros and their live points, each with the Feature it counts for (null when none): a Feature's own retro, or a
    // story's or bug's under it. An action item's retro belongs to no Feature. A theme is the point's current one: a merge repoints points.
    public const string RetroPoints =
        """
        WITH retro_root AS (
            SELECT rt.id AS retro_id, rt.created_at,
                   CASE WHEN w.type = 'feature' THEN w.id
                        WHEN w.type IN ('story', 'bug') AND pf.id IS NOT NULL THEN pf.id END AS root_id
            FROM retrospective rt
            LEFT JOIN work_item w ON w.id = rt.work_item_id
            LEFT JOIN work_item pf ON pf.id = w.parent_id AND pf.type = 'feature'
            WHERE rt.sprint_id = @sprintId AND rt.archived_at IS NULL
        )
        SELECT rr.root_id AS FeatureId, p.category AS Category, left(p.text, @textLength + 1) AS Text, th.name AS Theme
        FROM retro_root rr
        JOIN retro_point p ON p.retrospective_id = rr.retro_id AND p.archived_at IS NULL
        LEFT JOIN retro_theme th ON th.id = p.theme_id
        ORDER BY rr.created_at, rr.retro_id, p.position, p.id
        """;

    // retro_action.point_id is not read: retro_action_point replaced it.
    public const string Actions =
        """
        SELECT w.key AS Key, w.title AS Title, s.name AS State
        FROM retro_action ra
        JOIN work_item w ON w.id = ra.work_item_id
        JOIN state s ON s.id = w.state_id AND s.category <> 'removed'
        WHERE ra.raised_sprint_id = @sprintId
        ORDER BY w.number
        """;
}
