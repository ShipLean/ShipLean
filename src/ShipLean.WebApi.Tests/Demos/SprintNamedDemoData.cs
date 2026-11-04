using Dapper;
using Npgsql;
using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Demos;

// The sprint's real name and project key, nothing else: the run's tests are about who gets the link, not what the prompt says.
internal sealed class SprintNamedDemoData(NpgsqlDataSource dataSource) : ISprintDemoData
{
    public async Task<SprintDemoData> ReadAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync<(string Name, string Key)>(new CommandDefinition(
            "SELECT s.name, p.key FROM sprint s JOIN project p ON p.id = s.project_id WHERE s.id = @sprintId", new { sprintId }, cancellationToken: cancellationToken));
        return new SprintDemoData(
            new SprintDemoSprint(row.Name, string.Empty, new DateOnly(2100, 1, 1), new DateOnly(2100, 1, 14), true),
            new SprintDemoProject(row.Key, "Project", null, null, string.Empty),
            [],
            [],
            new SprintDemoRetroData([], [], [], []),
            [],
            false);
    }
}
