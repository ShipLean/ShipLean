using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Sprints;

namespace ShipLean.Application.Features.Sprints;

// The planning screen's velocity chip: writes project.velocity_override (null clears it). Callers check access first.
[RegisterPerRequest]
public sealed class VelocityOverrides(NpgsqlDataSource dataSource, TimeProvider clock, IProjectEvents events)
{
    private const decimal Largest = 999999.99m;

    public async Task PutAsync(Guid projectId, VelocityOverridePut put, CancellationToken cancellationToken)
    {
        if (put is null)
        {
            throw ApiException.Validation("The velocity override needs a body.");
        }

        if (put.VelocityOverride is < 0 or > Largest)
        {
            throw ApiException.Validation("The velocity override is zero or more points.");
        }

        if (put.VelocityOverride is { } points && decimal.Round(points, 2) != points)
        {
            throw ApiException.Validation("The velocity override has at most two decimal places.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var changed = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE project SET velocity_override = @value, updated_at = @now WHERE id = @projectId",
            new { projectId, value = put.VelocityOverride, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
        if (changed == 0)
        {
            throw ApiException.NotFound("There is no such project.");
        }

        await events.PublishAsync(new ProjectChanged(projectId));
    }
}
