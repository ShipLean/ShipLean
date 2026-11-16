using Agoda.IoC.Core;
using Dapper;
using Npgsql;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

[RegisterSingleton]
public sealed class PostgresRunLinkStore(NpgsqlDataSource dataSource) : IRunLinkStore
{
    public async Task<IReadOnlyList<RunLinkRow>> ReadAsync(string projectKey, string itemKey, DateTimeOffset notBefore, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT run_id, session_id, trace_ids, source, summary::text, fetched_at
            FROM inspection_run_links
            WHERE project_key = @projectKey AND item_key = @itemKey AND fetched_at > @notBefore
            """,
            connection);
        command.Parameters.AddWithValue("projectKey", projectKey);
        command.Parameters.AddWithValue("itemKey", itemKey);
        command.Parameters.AddWithValue("notBefore", notBefore.UtcDateTime);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<RunLinkRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new RunLinkRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetFieldValue<string[]>(2),
                reader.GetString(3),
                reader.GetString(4),
                new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc))));
        }
        return rows;
    }

    public async Task WriteAsync(string projectKey, string itemKey, IReadOnlyList<RunLinkRow> rows, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        foreach (var row in rows)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO inspection_run_links (project_key, item_key, run_id, session_id, trace_ids, source, summary, fetched_at)
                VALUES (@projectKey, @itemKey, @runId, @sessionId, @traceIds, @source, @summary::jsonb, @fetchedAt)
                ON CONFLICT (project_key, item_key, run_id)
                DO UPDATE SET session_id = EXCLUDED.session_id, trace_ids = EXCLUDED.trace_ids, source = EXCLUDED.source,
                              summary = EXCLUDED.summary, fetched_at = EXCLUDED.fetched_at
                """,
                new { projectKey, itemKey, runId = row.RunId, sessionId = row.SessionId, traceIds = row.TraceIds.ToArray(), source = row.Source, summary = row.Summary, fetchedAt = row.FetchedAt },
                cancellationToken: ct));
        }
    }

}
