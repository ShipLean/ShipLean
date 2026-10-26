using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Mcp.Planning.Lessons;

// Whether a work item is a Feature, for the plan_feature prompt, which must name a Feature and not a Story or Task. The caller checked access.
public sealed class FeatureKind(NpgsqlDataSource dataSource)
{
    public async Task<bool> IsFeatureAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM work_item WHERE id = @workItemId AND type = 'feature')", new { workItemId }, cancellationToken: cancellationToken));
    }
}
