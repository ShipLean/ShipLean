using System.Globalization;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Mcp.Sprint;

// What create_sprint needs that no shared read gives: the project's latest sprint (by end date) and how many there are.
[RegisterSingleton]
public sealed class SprintPlanReads(NpgsqlDataSource dataSource)
{
    // Null when the project has no live sprint yet.
    public async Task<LastSprint?> LastAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QueryFirstOrDefaultAsync<LastRow>(new CommandDefinition(
            """
            SELECT name, to_char(start_date, 'YYYY-MM-DD') AS startDate, to_char(end_date, 'YYYY-MM-DD') AS endDate,
                   (SELECT count(*) FROM sprint WHERE project_id = @projectId AND archived_at IS NULL)::int AS count
            FROM sprint WHERE project_id = @projectId AND archived_at IS NULL
            ORDER BY end_date DESC, start_date DESC LIMIT 1
            """,
            new { projectId }, cancellationToken: cancellationToken));
        return row is null ? null : new LastSprint(row.Name, Parse(row.StartDate), Parse(row.EndDate), row.Count);
    }

    private static DateOnly Parse(string day) => DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record LastRow(string Name, string StartDate, string EndDate, int Count);
}

// Count is how many live sprints the project has, this one included.
public sealed record LastSprint(string Name, DateOnly StartDate, DateOnly EndDate, int Count);
