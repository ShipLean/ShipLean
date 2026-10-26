using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Identity;

// IProjectAccess over SQL: the current user's role comes from live project_member rows (archived_at IS NULL). An item
// kind first resolves its project (archived items still resolve), then the same membership check applies. Instance
// admins get no implicit access: they join a project as admin like anyone else. A project the user is not a member
// of answers exactly like one that does not exist.
public sealed class ProjectAccess(NpgsqlDataSource dataSource, ICurrentUser currentUser, UserSessions sessions) : IProjectAccess
{
    private const string NotFoundMessage = "That project or item was not found.";

    private sealed record Found(Guid? ProjectId, string? Role);

    // Each statement returns the item's project and the current user's role there (null when not a member).
    private const string WorkItemSql =
        """
        SELECT x.project_id AS ProjectId, m.role AS Role FROM work_item x
        LEFT JOIN project_member m ON m.project_id = x.project_id AND m.user_id = @user AND m.archived_at IS NULL
        WHERE x.id = @id
        """;

    private const string SprintSql =
        """
        SELECT x.project_id AS ProjectId, m.role AS Role FROM sprint x
        LEFT JOIN project_member m ON m.project_id = x.project_id AND m.user_id = @user AND m.archived_at IS NULL
        WHERE x.id = @id
        """;

    private const string BoardSql =
        """
        SELECT x.project_id AS ProjectId, m.role AS Role FROM board x
        LEFT JOIN project_member m ON m.project_id = x.project_id AND m.user_id = @user AND m.archived_at IS NULL
        WHERE x.id = @id
        """;

    private const string CommentSql =
        """
        SELECT w.project_id AS ProjectId, m.role AS Role FROM comment x
        JOIN work_item w ON w.id = x.work_item_id
        LEFT JOIN project_member m ON m.project_id = w.project_id AND m.user_id = @user AND m.archived_at IS NULL
        WHERE x.id = @id
        """;

    private const string AttachmentSql =
        """
        SELECT w.project_id AS ProjectId, m.role AS Role FROM attachment x
        JOIN work_item w ON w.id = x.work_item_id
        LEFT JOIN project_member m ON m.project_id = w.project_id AND m.user_id = @user AND m.archived_at IS NULL
        WHERE x.id = @id
        """;

    // An import job has no project until its first import creates one.
    private const string ImportJobSql =
        """
        SELECT x.project_id AS ProjectId, m.role AS Role FROM import_job x
        LEFT JOIN project_member m ON m.project_id = x.project_id AND m.user_id = @user AND m.archived_at IS NULL
        WHERE x.id = @id
        """;

    private const string JiraConnectionSql =
        """
        SELECT x.project_id AS ProjectId, m.role AS Role FROM jira_connection x
        LEFT JOIN project_member m ON m.project_id = x.project_id AND m.user_id = @user AND m.archived_at IS NULL
        WHERE x.id = @id
        """;

    // A link's project is its source work item's.
    private const string LinkSql =
        """
        SELECT w.project_id AS ProjectId, m.role AS Role FROM work_item_link x
        JOIN work_item w ON w.id = x.source_id
        LEFT JOIN project_member m ON m.project_id = w.project_id AND m.user_id = @user AND m.archived_at IS NULL
        WHERE x.id = @id
        """;

    private static string SqlFor(ProjectItem item) => item switch
    {
        ProjectItem.WorkItem => WorkItemSql,
        ProjectItem.Sprint => SprintSql,
        ProjectItem.Board => BoardSql,
        ProjectItem.Comment => CommentSql,
        ProjectItem.Attachment => AttachmentSql,
        ProjectItem.ImportJob => ImportJobSql,
        ProjectItem.JiraConnection => JiraConnectionSql,
        ProjectItem.Link => LinkSql,
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, "Unknown project item kind."),
    };

    public async Task<ProjectRole> RequireAsync(Guid projectId, ProjectRole minimum, CancellationToken cancellationToken = default)
        => Check(await FindAsync(
            """
            SELECT p.id AS ProjectId, m.role AS Role FROM project p
            LEFT JOIN project_member m ON m.project_id = p.id AND m.user_id = @user AND m.archived_at IS NULL
            WHERE p.id = @id
            """, projectId, cancellationToken), minimum).Role;

    public async Task<(Guid ProjectId, ProjectRole Role)> RequireAsync(
        ProjectItem item, Guid id, ProjectRole minimum, CancellationToken cancellationToken = default)
        => Check(await FindAsync(SqlFor(item), id, cancellationToken), minimum);

    public async Task RequireInstanceAdminAsync(CancellationToken cancellationToken = default)
    {
        if (await sessions.GetFreshAsync(currentUser.Id, cancellationToken) is not { IsAdmin: true, Disabled: false })
        {
            throw ApiException.Forbidden("Only an instance admin can do that.");
        }
    }

    private async Task<Found?> FindAsync(string sql, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Found>(new CommandDefinition(
            sql, new { id, user = currentUser.Id }, cancellationToken: cancellationToken));
    }

    private static (Guid ProjectId, ProjectRole Role) Check(Found? found, ProjectRole minimum)
    {
        if (found is not { ProjectId: { } projectId, Role: { } text })
        {
            throw ApiException.NotFound(NotFoundMessage);
        }
        var role = Enum.Parse<ProjectRole>(text, ignoreCase: true);
        return role >= minimum ? (projectId, role) : throw ApiException.Forbidden(Refusal(minimum));
    }

    private static string Refusal(ProjectRole minimum)
        => minimum == ProjectRole.Admin ? "Only a project admin can do that." : "You need to be a member of this project to do that.";
}
