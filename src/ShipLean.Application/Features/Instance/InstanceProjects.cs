using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Instance;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Instance;

// Every project of the install, for an instance admin only (the caller is checked by [InstanceAdmin] on the controller;
// an instance admin gets no implicit access anywhere else), and joining one as its Admin.
[RegisterPerRequest]
public sealed class InstanceProjects(NpgsqlDataSource dataSource, ICurrentUser currentUser, IProjectEvents events, TimeProvider clock)
{
    private sealed record ProjectRow(Guid Id, string Key, string Name, int MemberCount, int ItemCount, string? YourRole);

    private sealed record AdminRow(Guid ProjectId, bool Disabled, Guid Id, string Name, string Initials, string AvatarColour, string? PhotoUrl);

    public async Task<IReadOnlyList<InstanceProjectDto>> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var projects = (await connection.QueryAsync<ProjectRow>(new CommandDefinition(
            """
            SELECT p.id, p.key, p.name,
                   (SELECT count(*) FROM project_member m WHERE m.project_id = p.id AND m.archived_at IS NULL)::int AS memberCount,
                   (SELECT count(*) FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.project_id = p.id AND s.category <> 'removed')::int AS itemCount,
                   (SELECT m.role FROM project_member m WHERE m.project_id = p.id AND m.user_id = @caller AND m.archived_at IS NULL) AS yourRole
            FROM project p
            ORDER BY p.key
            """,
            new { caller = currentUser.Id }, cancellationToken: cancellationToken))).AsList();
        var admins = (await connection.QueryAsync<AdminRow>(new CommandDefinition(
            $"""
            SELECT m.project_id AS projectId, (u.disabled_at IS NOT NULL) AS disabled, {UserRefs.Columns}
            FROM project_member m
            JOIN app_user u ON u.id = m.user_id
            WHERE m.role = 'admin' AND m.archived_at IS NULL
            ORDER BY lower(u.name) COLLATE "C", u.id
            """,
            cancellationToken: cancellationToken))).ToLookup(a => a.ProjectId);

        return
        [
            .. projects.Select(p => new InstanceProjectDto(
                p.Id, p.Key, p.Name, p.MemberCount,
                admins[p.Id].Select(a => new UserRef(a.Id, a.Name, a.Initials, a.AvatarColour, a.PhotoUrl)).ToList(),
                admins[p.Id].Any(a => !a.Disabled),
                p.ItemCount,
                p.YourRole switch { "admin" => ProjectRole.Admin, "member" => ProjectRole.Member, _ => null })),
        ];
    }

    // Makes the caller an Admin member: a new row is inserted, an archived one is un-archived and an existing one promoted.
    public async Task JoinAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            var now = clock.GetUtcNow();
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var locked = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                "SELECT id FROM project WHERE id = @projectId FOR NO KEY UPDATE", new { projectId }, transaction, cancellationToken: cancellationToken));
            if (locked is null)
            {
                throw ApiException.NotFound($"Project {projectId} was not found.");
            }
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO project_member (id, project_id, user_id, role, created_at, updated_at)
                VALUES (@id, @projectId, @userId, 'admin', @now, @now)
                ON CONFLICT (project_id, user_id) DO UPDATE SET role = 'admin', archived_at = NULL, updated_at = @now
                """,
                new { id = Guid.CreateVersion7(), projectId, userId = currentUser.Id, now },
                transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }
        await events.PublishAsync(new ProjectChanged(projectId));
    }
}
