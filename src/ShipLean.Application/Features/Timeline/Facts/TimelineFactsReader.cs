using Dapper;
using Npgsql;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Timeline.Facts;

// The facts over Postgres.
// TODO: reads only the Feature row and the project key; the full facts reader (stories, bugs, tasks, moves, Blocked tag changes,
// links, waves, plan applied, review events, sprints, people) replaces this.
public sealed class TimelineFactsReader(NpgsqlDataSource dataSource) : ITimelineFactsReader
{
    public async Task<TimelineFacts> ReadAsync(Guid projectId, Guid featureId, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync<FeatureRow>(new CommandDefinition(
            """
            SELECT p.key AS projectKey, f.id, f.key, f.title, s.name AS stateName, s.category AS stateCategory,
                   f.created_at AS createdAt, f.created_by AS createdBy, e.key AS epicKey, e.title AS epicTitle
            FROM work_item f
            JOIN project p ON p.id = f.project_id
            JOIN state s ON s.id = f.state_id
            LEFT JOIN work_item e ON e.id = f.parent_id AND e.type = 'epic'
            WHERE f.project_id = @projectId AND f.id = @featureId AND f.type = 'feature'
            """,
            new { projectId, featureId }, cancellationToken: cancellationToken));

        var feature = new TimelineFeatureFact(
            row.Id, row.Key, row.Title, row.StateName, row.StateCategory, row.CreatedAt, row.CreatedBy,
            row.EpicKey is null || row.EpicTitle is null ? null : new TimelineEpicFact(row.EpicKey, row.EpicTitle));
        return new TimelineFacts(
            row.ProjectKey, feature, null, [], [], [], [], [], new Dictionary<string, string>(), [], [], new Dictionary<Guid, string>());
    }

    private sealed record FeatureRow(
        string ProjectKey,
        Guid Id,
        string Key,
        string Title,
        string StateName,
        StateCategory StateCategory,
        DateTimeOffset CreatedAt,
        Guid? CreatedBy,
        string? EpicKey,
        string? EpicTitle);
}
