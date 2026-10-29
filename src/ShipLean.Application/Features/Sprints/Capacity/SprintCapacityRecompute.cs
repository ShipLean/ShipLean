using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Holidays;
using ShipLean.Contracts.Sprints;
using ShipLean.Core;
using ShipLean.Core.Sprints;

namespace ShipLean.Application.Features.Sprints;

// ISprintCapacityRecompute over Postgres: recomputes capacity_points of the project's sprints that have not ended and have a
// capacity_velocity, and clamps a typed days value to the new working-day count. Joins the caller's transaction when given one.
public sealed class SprintCapacityRecompute(NpgsqlDataSource dataSource, TimeProvider clock, IPublicHolidays holidays) : ISprintCapacityRecompute
{
    public async Task<IReadOnlyList<Guid>> RecomputeAsync(Guid projectId, DbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        if (transaction is not null)
        {
            return await RunAsync(transaction.Connection!, transaction, projectId, cancellationToken);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var own = await connection.BeginTransactionAsync(cancellationToken);
        var changed = await RunAsync(connection, own, projectId, cancellationToken);
        await own.CommitAsync(cancellationToken);
        return changed;
    }

    private async Task<IReadOnlyList<Guid>> RunAsync(DbConnection connection, DbTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await CapacityProject.LoadAsync(connection, transaction, projectId, cancellationToken);
        var sprints = (await connection.QueryAsync<SprintDates>(new CommandDefinition(
            """
            SELECT id, to_char(start_date, 'YYYY-MM-DD') AS startDate, to_char(end_date, 'YYYY-MM-DD') AS endDate, capacity_velocity AS velocity
            FROM sprint
            WHERE project_id = @projectId AND archived_at IS NULL AND capacity_velocity IS NOT NULL AND end_date >= @today
            ORDER BY start_date FOR UPDATE
            """,
            new { projectId, today = SprintLabels.Today(clock) }, transaction, cancellationToken: cancellationToken))).ToList();
        var now = clock.GetUtcNow();
        var changed = new List<Guid>();
        foreach (var sprint in sprints)
        {
            var calendar = await CapacityCalendar.LoadAsync(holidays, project.HolidayCountry, project.Days, sprint.Start, sprint.End, cancellationToken);
            var rows = (await connection.QueryAsync<StoredRow>(new CommandDefinition(
                "SELECT id, days, capacity_points AS points FROM sprint_capacity WHERE sprint_id = @id AND archived_at IS NULL",
                new { sprint.Id }, transaction, cancellationToken: cancellationToken))).ToList();
            var touched = false;
            foreach (var row in rows)
            {
                var days = row.Days is { } typed ? Math.Min(typed, calendar.WorkingDays) : (decimal?)null;
                var points = CapacityPoints.For(sprint.Velocity, rows.Count, calendar.Effective(days), calendar.WorkingDays);
                if (points == row.Points && days == row.Days)
                {
                    continue;
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE sprint_capacity SET capacity_points = @points, days = @days, updated_at = @now WHERE id = @id",
                    new { row.Id, points, days, now }, transaction, cancellationToken: cancellationToken));
                touched = true;
            }

            if (touched)
            {
                changed.Add(sprint.Id);
            }
        }

        return changed;
    }

    private sealed record SprintDates(Guid Id, string StartDate, string EndDate, decimal Velocity)
    {
        public DateOnly Start => DateOnly.ParseExact(StartDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        public DateOnly End => DateOnly.ParseExact(EndDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record StoredRow(Guid Id, decimal? Days, decimal Points);
}
