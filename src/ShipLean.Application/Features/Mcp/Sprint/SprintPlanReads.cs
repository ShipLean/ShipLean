using System.Globalization;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Core.Sprints;

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

    // The project's sprint naming convention and the names of its live sprints.
    public async Task<SprintNamingState> NamingAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var naming = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT sprint_naming FROM project WHERE id = @projectId", new { projectId }, cancellationToken: cancellationToken));
        var names = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT name FROM sprint WHERE project_id = @projectId AND archived_at IS NULL", new { projectId }, cancellationToken: cancellationToken));
        return new SprintNamingState(SprintNaming.FromColumn(naming), [.. names]);
    }

    private static DateOnly Parse(string day) => DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record LastRow(string Name, string StartDate, string EndDate, int Count);
}

// Count is how many live sprints the project has, this one included.
public sealed record LastSprint(string Name, DateOnly StartDate, DateOnly EndDate, int Count);

public sealed record SprintNamingState(SprintNamingConvention Convention, IReadOnlyList<string> LiveNames);
