using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Realtime;

// The hub's own membership check, for a user named explicitly. IProjectAccess reads the user from the request's
// IHttpContextAccessor, which SignalR does not guarantee inside a hub, so the hub takes the user from Context.User
// and asks here. The rule is ProjectAccess's: a live project_member row of any role; a non-member and an unknown
// project get the same not_found.
[RegisterSingleton]
public class HubMembership(NpgsqlDataSource dataSource)
{
    private sealed record Pair(Guid ProjectId, Guid UserId);

    public virtual async Task RequireMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var member = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1 FROM project p
                JOIN project_member m ON m.project_id = p.id AND m.user_id = @userId AND m.archived_at IS NULL
                WHERE p.id = @projectId)
            """, new { projectId, userId }, cancellationToken: cancellationToken));
        if (!member)
        {
            throw ApiException.NotFound("That project or item was not found.");
        }
    }

    // The (project, user) pairs that RequireMemberAsync would refuse, in one query, for HubSessionSweep.
    public virtual async Task<IReadOnlyList<(Guid ProjectId, Guid UserId)>> NotMembersAsync(
        IReadOnlyList<(Guid ProjectId, Guid UserId)> pairs, CancellationToken cancellationToken)
    {
        if (pairs.Count == 0)
        {
            return [];
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var gone = await connection.QueryAsync<Pair>(new CommandDefinition(
            """
            SELECT pair.project_id AS "ProjectId", pair.user_id AS "UserId"
            FROM unnest(@projectIds, @userIds) AS pair(project_id, user_id)
            WHERE NOT EXISTS (
                SELECT 1 FROM project p
                JOIN project_member m ON m.project_id = p.id AND m.user_id = pair.user_id AND m.archived_at IS NULL
                WHERE p.id = pair.project_id)
            """,
            new { projectIds = pairs.Select(p => p.ProjectId).ToArray(), userIds = pairs.Select(p => p.UserId).ToArray() },
            cancellationToken: cancellationToken));
        return gone.Select(p => (p.ProjectId, p.UserId)).ToList();
    }
}
