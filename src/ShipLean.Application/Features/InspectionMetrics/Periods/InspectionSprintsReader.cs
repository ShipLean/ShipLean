using System.Globalization;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics.Periods;

// The project and every live (not archived) sprint of it, by day, oldest first. Day boundaries come back as 'YYYY-MM-DD' text so no
// session zone is involved.
public sealed class InspectionSprintsReader(NpgsqlDataSource dataSource)
{
    private const string ProjectSql = "SELECT id, key, name FROM project WHERE id = @projectId";

    private const string SprintsSql =
        """
        SELECT id, name, to_char(start_date, 'YYYY-MM-DD') AS startDate, to_char(end_date, 'YYYY-MM-DD') AS endDate
        FROM sprint
        WHERE project_id = @projectId AND archived_at IS NULL
        ORDER BY start_date, name
        """;

    public async Task<InspectionSprints> ReadAsync(Guid projectId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var project = await connection.QuerySingleAsync<InspectionProjectDto>(new CommandDefinition(ProjectSql, new { projectId }, cancellationToken: ct));
        var rows = await connection.QueryAsync<SprintRow>(new CommandDefinition(SprintsSql, new { projectId }, cancellationToken: ct));
        return new InspectionSprints(project, [.. rows.Select(row => new InspectionSprint(row.Id, row.Name, ParseDay(row.StartDate), ParseDay(row.EndDate)))]);
    }

    private static DateOnly ParseDay(string day) => DateOnly.ParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record SprintRow(Guid Id, string Name, string StartDate, string EndDate);
}
