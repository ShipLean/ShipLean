using Agoda.IoC.Core;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Users;
using ShipLean.Contracts.Users.Admin;
using ShipLean.Core;

namespace ShipLean.Application.Features.Users.Admin;

// The instance admin's Users tab: the table, and adding a user. The caller is checked by [InstanceAdmin] on the
// controller. Every write goes through IUserAccounts and leaves one audit line: who, what, to whom, never a password or
// an email. Adding a user is create (no password), promote, Member rows in the chosen projects (which the caller must
// administer), and the one-time password last, so an aborted request leaves a user that Set up sign-in can recover. In proxy mode there is no one-time password.
[RegisterPerRequest]
public sealed class UserAdmin(
    NpgsqlDataSource dataSource, ICurrentUser currentUser, IProjectAccess access, IUserDirectory directory, IUserAccounts accounts, IProjectEvents events, TimeProvider clock, ProxySignInSettings proxy, ILogger<UserAdmin> logger)
{
    private sealed record UserRowDto(Guid Id, string Name, string Email, string Initials, string AvatarColour, string? PhotoUrl, bool IsInstanceAdmin, string Status, DateTimeOffset? LastSignInAt);

    private sealed record MembershipRow(Guid UserId, Guid Id, string Key, string Name, string Role, bool SoleActiveAdmin);

    public async Task<AdminUsersDto> ListAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var users = (await connection.QueryAsync<UserRowDto>(new CommandDefinition(
            $"""
            SELECT u.id, u.name, u.email, u.initials, u.avatar_colour AS avatarColour, {UserRefs.PhotoUrlSql} AS photoUrl,
                   u.is_admin AS isInstanceAdmin, {UserStatusRule.Sql} AS status, u.last_sign_in_at AS lastSignInAt
            FROM app_user u
            ORDER BY lower(u.name) COLLATE "C", u.id
            """,
            new { now = clock.GetUtcNow() }, cancellationToken: cancellationToken))).AsList();
        var memberships = (await connection.QueryAsync<MembershipRow>(new CommandDefinition(
            """
            SELECT m.user_id AS userId, p.id, p.key, p.name, m.role,
                   (m.role = 'admin' AND NOT EXISTS (
                       SELECT 1 FROM project_member o JOIN app_user ou ON ou.id = o.user_id
                       WHERE o.project_id = m.project_id AND o.role = 'admin' AND o.archived_at IS NULL
                         AND o.user_id <> m.user_id AND ou.disabled_at IS NULL)) AS soleActiveAdmin
            FROM project_member m JOIN project p ON p.id = m.project_id
            WHERE m.archived_at IS NULL
            ORDER BY p.key
            """,
            cancellationToken: cancellationToken))).ToLookup(m => m.UserId);
        var administered = await connection.QueryAsync<AdminProjectChoiceDto>(new CommandDefinition(
            """
            SELECT p.id, p.key, p.name FROM project p
            JOIN project_member m ON m.project_id = p.id AND m.user_id = @caller AND m.role = 'admin' AND m.archived_at IS NULL
            ORDER BY p.key
            """,
            new { caller = currentUser.Id }, cancellationToken: cancellationToken));

        return new AdminUsersDto([.. users.Select(u => ToDto(u, memberships[u.Id]))], administered.AsList());
    }

    public async Task<AddUserResponse> AddAsync(AddUserRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw ApiException.Validation("The user needs an email and a name.");
        }
        var projectIds = (request.ProjectIds ?? []).Distinct().ToList();
        foreach (var projectId in projectIds)
        {
            await access.RequireAsync(projectId, ProjectRole.Admin, cancellationToken);
        }
        var created = await directory.CreateAsync(request.Email, request.Name, cancellationToken);
        if (request.IsInstanceAdmin)
        {
            await accounts.SetInstanceAdminAsync(created.Id, true, cancellationToken);
        }
        if (projectIds.Count > 0)
        {
            await AddMemberAsync(created.Id, projectIds, cancellationToken);
            foreach (var projectId in projectIds)
            {
                await events.PublishAsync(new ProjectChanged(projectId));
            }
        }
        // Proxy mode has no passwords: the user signs in through the proxy on first visit.
        var oneTime = proxy.IsProxy ? null : await accounts.IssueOneTimePasswordAsync(created.Id, cancellationToken);
        Audit("add", created.Id);
        var listed = (await ListAsync(cancellationToken)).Users.Single(u => u.Id == created.Id);
        return new AddUserResponse(listed, oneTime);
    }

    public async Task<OneTimePasswordDto> IssueOneTimePasswordAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == currentUser.Id)
        {
            throw ApiException.Validation("Change your own password on Your profile.");
        }
        var issued = await accounts.IssueOneTimePasswordAsync(userId, cancellationToken);
        Audit("one-time-password", userId);
        return issued;
    }

    public async Task UnlockAsync(Guid userId, CancellationToken cancellationToken)
    {
        await accounts.UnlockAsync(userId, cancellationToken);
        Audit("unlock", userId);
    }

    public async Task SetDisabledAsync(Guid userId, bool disabled, CancellationToken cancellationToken)
    {
        await accounts.SetDisabledAsync(userId, disabled, cancellationToken);
        Audit(disabled ? "disable" : "enable", userId);
        await PublishProjectsOfAsync(userId, cancellationToken);
    }

    public async Task SetInstanceAdminAsync(Guid userId, bool isInstanceAdmin, CancellationToken cancellationToken)
    {
        await accounts.SetInstanceAdminAsync(userId, isInstanceAdmin, cancellationToken);
        Audit(isInstanceAdmin ? "promote" : "demote", userId);
    }

    public async Task ChangeEmailAsync(Guid userId, string email, CancellationToken cancellationToken)
    {
        if (userId == currentUser.Id)
        {
            throw ApiException.Validation("Change your own email on Your profile.");
        }
        await accounts.ChangeEmailAsync(userId, email, cancellationToken);
        Audit("change-email", userId);
        await PublishProjectsOfAsync(userId, cancellationToken);
    }

    private void Audit(string action, Guid targetId)
        => logger.LogInformation("Instance user admin: {Action} on user {TargetId} by {ActorId}", action, targetId, currentUser.Id);

    // The Members list shows a user's name, email and status, so every project the user belongs to refreshes.
    private async Task PublishProjectsOfAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var projectIds = await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT project_id FROM project_member WHERE user_id = @userId AND archived_at IS NULL", new { userId }, cancellationToken: cancellationToken));
        foreach (var projectId in projectIds)
        {
            await events.PublishAsync(new ProjectChanged(projectId));
        }
    }

    private async Task AddMemberAsync(Guid userId, List<Guid> projectIds, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        foreach (var projectId in projectIds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO project_member (id, project_id, user_id, role, created_at, updated_at)
                VALUES (@id, @projectId, @userId, 'member', @now, @now)
                """,
                new { id = Guid.CreateVersion7(), projectId, userId, now }, transaction, cancellationToken: cancellationToken));
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private AdminUserDto ToDto(UserRowDto user, IEnumerable<MembershipRow> memberships) => new(
        user.Id, user.Name, user.Email, user.Initials, user.AvatarColour, user.PhotoUrl, user.IsInstanceAdmin,
        UserStatusRule.Parse(user.Status),
        user.LastSignInAt is { } at ? DisplayDate.Relative(at, clock) : null,
        [.. memberships.Select(m => new AdminUserProjectDto(m.Id, m.Key, m.Name, Enum.Parse<ProjectRole>(m.Role, ignoreCase: true), m.SoleActiveAdmin))]);
}
