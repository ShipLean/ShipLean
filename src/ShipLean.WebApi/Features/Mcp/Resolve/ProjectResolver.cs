using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.Mcp.Resolve;

// Turns the `project` an agent passes (a key, or nothing) into a project the user is a member of. A key of a project
// the user is not in answers exactly like an unknown key. With no key, a read falls back to the most recent project
// (the first of the user's recentProjects they still belong to, else their only project, else the first by key); a
// write has no safe fallback, so it needs a key unless the user is in exactly one project.
public sealed class ProjectResolver(NpgsqlDataSource dataSource, ICurrentUser currentUser)
{
    public async Task<ResolvedProject> ResolveAsync(string? key, bool forWrite, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IReadOnlyList<ProjectRow> projects = [.. await connection.QueryAsync<ProjectRow>(new CommandDefinition(
            """
            SELECT p.id, p.key, p.name, m.role AS roleText
            FROM project p JOIN project_member m ON m.project_id = p.id AND m.user_id = @me AND m.archived_at IS NULL
            ORDER BY p.key
            """,
            new { me = currentUser.Id }, cancellationToken: cancellationToken))];

        if (!string.IsNullOrWhiteSpace(key))
        {
            var match = projects.FirstOrDefault(p => NameMatcher.Same(p.Key, key));
            return (match ?? throw ApiException.NotFound("No project with that key was found.")).ToResolved();
        }

        if (projects.Count == 0)
        {
            throw ApiException.NotFound("You are not a member of any project.");
        }
        if (projects.Count == 1)
        {
            return projects[0].ToResolved();
        }
        if (forWrite)
        {
            throw ApiException.Validation($"You are in {projects.Count} projects: name a project (one of {string.Join(", ", projects.Select(p => p.Key))}).", "project");
        }

        foreach (var id in await RecentAsync(connection, cancellationToken))
        {
            if (projects.FirstOrDefault(p => p.Id == id) is { } recent)
            {
                return recent.ToResolved();
            }
        }
        return projects[0].ToResolved();
    }

    private async Task<IReadOnlyList<Guid>> RecentAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var json = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT json::text FROM user_preference WHERE user_id = @me AND key = 'recentProjects'", new { me = currentUser.Id }, cancellationToken: cancellationToken));
        return RecentProjects.Parse(json);
    }

    private sealed record ProjectRow(Guid Id, string Key, string Name, string RoleText)
    {
        public ResolvedProject ToResolved() => new(Id, Key, Name, Enum.Parse<ProjectRole>(RoleText, true));
    }
}
