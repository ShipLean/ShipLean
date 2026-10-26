using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.Mcp.Resolve;

// Turns a work item key ("ship-123") into an item the user may act on: key to id by SQL among the projects the user is in, then the access rule. An
// unknown key and a key in a project the user is not in are both not_found.
public sealed partial class ItemResolver(NpgsqlDataSource dataSource, IProjectAccess access, ICurrentUser currentUser)
{
    [GeneratedRegex("^[A-Z]{2,4}-[0-9]+$")]
    private static partial Regex KeyShape();

    // project: when the caller already resolved a project, a key from another project is a validation error.
    public async Task<ResolvedItem> ResolveAsync(string key, ProjectRole minimum, Guid? project = null, string field = "item", CancellationToken cancellationToken = default)
    {
        var wanted = (key ?? string.Empty).Trim().ToUpperInvariant();
        if (!KeyShape().IsMatch(wanted))
        {
            throw ApiException.Validation($"\"{key}\" is not a work item key like SHIP-123.", field);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var id = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT w.id FROM work_item w
            JOIN project_member m ON m.project_id = w.project_id AND m.user_id = @me AND m.archived_at IS NULL
            WHERE w.key = @wanted
            """,
            new { wanted, me = currentUser.Id }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("No work item with that key was found.");

        var (projectId, role) = await access.RequireAsync(ProjectItem.WorkItem, id, minimum, cancellationToken);
        return project is { } expected && expected != projectId
            ? throw ApiException.Validation($"{wanted} is not in the project you named.", field)
            : new ResolvedItem(id, wanted, projectId, role);
    }
}
