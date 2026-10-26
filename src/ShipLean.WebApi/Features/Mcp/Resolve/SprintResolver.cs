using Dapper;
using Npgsql;
using ShipLean.Application.Features.Mcp.Orient;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.Mcp.Resolve;

// Turns "current", "next" or a sprint name into a sprint of the project. Current and next are derived from the dates
// (today in UTC); archived sprints never match.
public sealed class SprintResolver(NpgsqlDataSource dataSource, IProjectAccess access, TimeProvider clock)
{
    public async Task<ResolvedSprint> ResolveAsync(Guid projectId, string sprint, string field = "sprint", CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        var wanted = (sprint ?? string.Empty).Trim();
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        if (NameMatcher.Same(wanted, "current"))
        {
            return (await connection.QueryFirstOrDefaultAsync<SprintRow>(new CommandDefinition(
                       $"SELECT id, name, true AS active FROM sprint s WHERE project_id = @projectId AND {CurrentSprintSql.Where} ORDER BY start_date LIMIT 1",
                       new { projectId, today }, cancellationToken: cancellationToken)))?.ToResolved()
                   ?? throw ApiException.NotFound("No sprint is running today.");
        }
        if (NameMatcher.Same(wanted, "next"))
        {
            return (await connection.QueryFirstOrDefaultAsync<SprintRow>(new CommandDefinition(
                       "SELECT id, name, true AS active FROM sprint WHERE project_id = @projectId AND archived_at IS NULL AND start_date > @today ORDER BY start_date LIMIT 1",
                       new { projectId, today }, cancellationToken: cancellationToken)))?.ToResolved()
                   ?? throw ApiException.NotFound("No sprint is planned after today.");
        }

        var sprints = (await connection.QueryAsync<SprintRow>(new CommandDefinition(
            "SELECT id, name, (archived_at IS NULL) AS active FROM sprint WHERE project_id = @projectId", new { projectId }, cancellationToken: cancellationToken))).ToList();
        var (match, exact) = NameMatcher.Pick(
            [.. sprints.Where(s => s.Active).Select(s => s.ToResolved())], [.. sprints.Where(s => !s.Active).Select(s => s.ToResolved())],
            wanted, field, "sprint", s => [s.Name], s => [s.Name], s => s.Name);
        return match with { Exact = exact };
    }

    // The sprint by id when it is still a live sprint of the project (not archived, not another project's); null otherwise. For
    // callers that hold an id from earlier and need to know it is still usable; nothing here throws for a sprint that went away.
    public async Task<ResolvedSprint?> FindLiveAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return (await connection.QueryFirstOrDefaultAsync<SprintRow>(new CommandDefinition(
            "SELECT id, name, true AS active FROM sprint WHERE id = @sprintId AND project_id = @projectId AND archived_at IS NULL",
            new { sprintId, projectId }, cancellationToken: cancellationToken)))?.ToResolved();
    }

    private sealed record SprintRow(Guid Id, string Name, bool Active)
    {
        public ResolvedSprint ToResolved() => new(Id, Name);
    }
}
