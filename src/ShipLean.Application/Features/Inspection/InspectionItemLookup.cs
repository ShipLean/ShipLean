using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection;

// The item key's project and creation date, for a member of the project (K3). A project key and item key that do not belong together are not_found.
[RegisterPerRequest]
public sealed class InspectionItemLookup(NpgsqlDataSource dataSource, IProjectAccess access) : IInspectionItemLookup
{
    public async Task<InspectionItemInfo> RequireAsync(string projectKey, string itemKey, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var found = await connection.QuerySingleOrDefaultAsync<ItemRow>(new CommandDefinition(
            """
            SELECT p.id AS projectId, p.key AS projectKey, w.key AS itemKey, w.created_at AS createdAt
            FROM work_item w JOIN project p ON p.id = w.project_id
            WHERE p.key = @projectKey AND w.key = @itemKey
            """,
            new { projectKey, itemKey }, cancellationToken: ct));
        if (found is null)
        {
            // Unknown, or somewhere the caller cannot see: the same answer.
            throw ApiException.NotFound("Work item not found.");
        }

        await access.RequireAsync(found.ProjectId, ProjectRole.Member, ct);
        return new InspectionItemInfo(found.ProjectId, found.ProjectKey, found.ItemKey, found.CreatedAt);
    }

    private sealed record ItemRow(Guid ProjectId, string ProjectKey, string ItemKey, DateTimeOffset CreatedAt);
}
