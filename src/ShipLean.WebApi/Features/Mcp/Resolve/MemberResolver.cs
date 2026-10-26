using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.Mcp.Resolve;

// Turns "me", an email, a name or initials into an active member of the project. An email matches exactly; a name
// matches exactly or by the start of the name or of any word in it ("Priya", "Okafor").
public sealed class MemberResolver(NpgsqlDataSource dataSource, IProjectAccess access, ICurrentUser currentUser)
{
    public async Task<ResolvedMember> ResolveAsync(Guid projectId, string member, string field = "assignee", CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var members = (await connection.QueryAsync<MemberRow>(new CommandDefinition(
            """
            SELECT u.id, u.name, u.email, u.initials, (m.archived_at IS NULL AND u.disabled_at IS NULL) AS active
            FROM project_member m JOIN app_user u ON u.id = m.user_id
            WHERE m.project_id = @projectId
            """,
            new { projectId }, cancellationToken: cancellationToken))).ToList();
        var live = members.Where(m => m.Active).Select(m => m.ToResolved(true)).ToList();
        var gone = members.Where(m => !m.Active).Select(m => m.ToResolved(true)).ToList();

        var wanted = (member ?? string.Empty).Trim();
        if (NameMatcher.Same(wanted, "me"))
        {
            return live.FirstOrDefault(m => m.Id == currentUser.Id) ?? throw ApiException.NotFound("You are not an active member of that project.");
        }
        var (match, exact) = NameMatcher.Pick(
            live, gone, wanted, field, "member",
            m => [m.Email, m.Name, m.Initials, Display(m)],
            m => m.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Append(m.Name),
            Display);
        return match with { Exact = exact };
    }

    // Two members can share a name, so a candidate reads "Name <email>", which resolves back to that member exactly.
    private static string Display(ResolvedMember m) => $"{m.Name} <{m.Email}>";

    private sealed record MemberRow(Guid Id, string Name, string Email, string Initials, bool Active)
    {
        public ResolvedMember ToResolved(bool exact) => new(Id, Name, Email, Initials, exact);
    }
}
