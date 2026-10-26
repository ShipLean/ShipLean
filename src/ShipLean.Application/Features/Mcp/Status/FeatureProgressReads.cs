using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Mcp.Status;

// The item feature_progress is about: the one row IWorkTreeReads has no entry for (it reads below an item, never the item).
[RegisterSingleton]
public sealed class FeatureProgressReads(NpgsqlDataSource dataSource)
{
    // Throws ApiException.NotFound (unknown item). Callers check project access first.
    public async Task<ProgressHead> HeadAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ProgressHead>(new CommandDefinition(
            "SELECT p.key AS projectKey, w.title, w.type, s.name AS state, s.category AS stateCategory FROM work_item w JOIN state s ON s.id = w.state_id JOIN project p ON p.id = w.project_id WHERE w.id = @itemId",
            new { itemId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("No work item with that key was found.");
    }
}
