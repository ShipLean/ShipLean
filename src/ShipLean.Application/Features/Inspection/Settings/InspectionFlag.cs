using Agoda.IoC.Core;
using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Inspection.Settings;

// Whether an item page shows Agent runs: Inspection is on for the instance and for the item's project. No access check: the caller
// already read the item.
[RegisterPerRequest]
public sealed class InspectionFlag(InspectionOptions options, NpgsqlDataSource dataSource)
{
    public async Task<bool> IsOnAsync(Guid projectId, CancellationToken ct)
    {
        if (!options.Enabled)
        {
            return false;
        }
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT COALESCE((SELECT enabled FROM inspection_project_settings WHERE project_id = @projectId), false)", new { projectId }, cancellationToken: ct));
    }
}
