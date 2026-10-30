using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Holidays;
using ShipLean.Contracts.Sprints;

namespace ShipLean.Application.Features.Sprints;

// IHolidaysChanged: when a country's holidays change, recomputes the capacity of each project that uses that country and tells
// its open boards which sprints moved. Resolved from a fresh scope by the refresh job.
public sealed class SprintHolidaysChanged(NpgsqlDataSource dataSource, ISprintCapacityRecompute recompute, IProjectEvents events, ILogger<SprintHolidaysChanged> logger) : IHolidaysChanged
{
    public async Task ChangedAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var projects = (await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT id FROM project WHERE holiday_country = @countryCode::char(2) ORDER BY id",
            new { countryCode }, cancellationToken: cancellationToken))).ToList();
        foreach (var projectId in projects)
        {
            try
            {
                foreach (var sprintId in await recompute.RecomputeAsync(projectId, null, cancellationToken))
                {
                    await events.PublishAsync(new SprintChanged(projectId, sprintId));
                }
            }
            catch (Exception failed) when (failed is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // One project's failure must not leave the others on stale capacity.
                logger.LogError(failed, "Recomputing the capacity of project {ProjectId} after the {Country} holidays changed failed", projectId, countryCode);
            }
        }
    }
}
