using Dapper;
using Npgsql;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Mcp.Planning.Status;

// The status reads over Postgres. Review and commit text is tested with PlanMarkers in C#; SQL only narrows comments by the review prefix.
public sealed class PlanStatusReads(NpgsqlDataSource dataSource) : IPlanStatusReads
{
    // Per task, the first revision into an InProgress state and the last into a Completed one, by changed_at, as MetricsSql.History does.
    private const string Moves =
        """
        SELECT r.work_item_id AS taskId,
               MIN(r.changed_at) FILTER (WHERE s.category = 'inProgress') AS startedAt,
               MAX(r.changed_at) FILTER (WHERE s.category = 'completed') AS finishedAt
        FROM revision r
        CROSS JOIN LATERAL jsonb_array_elements(r.changes) AS c(value)
        JOIN state s ON s.id::text = (c.value -> 'to') #>> '{}'
        WHERE r.work_item_id = ANY(@taskIds) AND c.value ->> 'field' = @stateField AND s.category IN ('inProgress', 'completed')
        GROUP BY r.work_item_id
        """;

    public async Task<IReadOnlyDictionary<Guid, PlanTaskActivity>> ActivityAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken = default)
    {
        var ids = taskIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, PlanTaskActivity>();
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var moves = await connection.QueryAsync<MoveRow>(new CommandDefinition(
            Moves, new { taskIds = ids, stateField = FieldIds.State }, cancellationToken: cancellationToken));
        var links = await connection.QueryAsync<LinkRow>(new CommandDefinition(
            "SELECT source_id AS taskId, url, title FROM work_item_link WHERE source_id = ANY(@taskIds) AND link_type = 'url' AND archived_at IS NULL AND title IS NOT NULL ORDER BY created_at, id",
            new { taskIds = ids }, cancellationToken: cancellationToken));
        var comments = await connection.QueryAsync<CommentRow>(new CommandDefinition(
            "SELECT work_item_id AS taskId, left(body, @prefixLength) AS body FROM comment WHERE work_item_id = ANY(@taskIds) AND archived_at IS NULL AND body LIKE @prefix",
            new { taskIds = ids, prefix = PlanMarkers.ReviewPrefix + "%", prefixLength = PlanMarkers.ReviewPrefix.Length }, cancellationToken: cancellationToken));

        var movesByTask = moves.ToDictionary(row => row.TaskId);
        var linksByTask = links.ToLookup(row => row.TaskId);
        var reviewsByTask = comments.Where(row => PlanMarkers.IsReview(row.Body)).ToLookup(row => row.TaskId);
        return ids.ToDictionary(id => id, id => new PlanTaskActivity(
            movesByTask.GetValueOrDefault(id)?.StartedAt,
            movesByTask.GetValueOrDefault(id)?.FinishedAt,
            [.. linksByTask[id].Select(Commit).OfType<PlanCommitLink>()],
            reviewsByTask[id].Count()));
    }

    private static PlanCommitLink? Commit(LinkRow row)
        => PlanMarkers.TryReadCommit(row.Title, out var sha, out var subject) ? new PlanCommitLink(sha, subject, row.Url) : null;

    private sealed record MoveRow(Guid TaskId, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt);

    private sealed record LinkRow(Guid TaskId, string Url, string Title);

    private sealed record CommentRow(Guid TaskId, string Body);
}
