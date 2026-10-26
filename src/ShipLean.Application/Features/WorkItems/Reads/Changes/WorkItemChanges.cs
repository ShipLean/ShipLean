using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems.Reads.Changes;

namespace ShipLean.Application.Features.WorkItems.Reads.Changes;

// Changes since a cursor over Postgres. Revisions (kind 0) and comments (kind 1) are one stream ordered by
// (time, kind, tiebreak): a revision's tiebreak is its item id and zero-padded rev, a comment's is its id.
public sealed class WorkItemChanges(NpgsqlDataSource dataSource) : IWorkItemChanges
{
    private const int MaxLimit = 500;

    private const string Sql =
        """
        SELECT * FROM (
            SELECT w.id AS workItemId, w.key, w.title, 0 AS kind,
                   w.id::text || ':' || lpad(r.rev::text, 10, '0') AS tie,
                   r.changed_by AS actorId, u.name AS actorName, r.changed_at AS at,
                   r.changes::text AS changes, NULL::text AS body, r.rev AS revision
            FROM revision r
            JOIN work_item w ON w.id = r.work_item_id
            LEFT JOIN app_user u ON u.id = r.changed_by
            WHERE w.project_id = @projectId
              AND (@mine::uuid IS NULL OR w.assigned_to = @mine::uuid OR w.created_by = @mine::uuid)
            UNION ALL
            SELECT w.id, w.key, w.title, 1, c.id::text,
                   c.author_id, u.name, c.created_at, NULL::text, c.body, NULL::int
            FROM comment c
            JOIN work_item w ON w.id = c.work_item_id
            LEFT JOIN app_user u ON u.id = c.author_id
            WHERE w.project_id = @projectId AND c.archived_at IS NULL
              AND (@mine::uuid IS NULL OR w.assigned_to = @mine::uuid OR w.created_by = @mine::uuid)
        ) AS entry
        WHERE @hasCursor = false OR (entry.at, entry.kind, entry.tie COLLATE "C") > (@cursorAt::timestamptz, @cursorKind, @cursorTie::text COLLATE "C")
        ORDER BY entry.at, entry.kind, entry.tie COLLATE "C"
        LIMIT @take
        """;

    public async Task<ChangesPageDto> SinceAsync(Guid projectId, string? cursor, int limit, Guid? mineUserId, CancellationToken cancellationToken = default)
    {
        CheckLimit(limit);
        return await PageAsync(projectId, ChangeCursor.Decode(cursor), cursor, limit, mineUserId, cancellationToken);
    }

    // An empty tiebreak sorts before every real one, so "after (since, revision, empty)" keeps every entry at or after the time.
    // The cursor of an empty page is the time itself with a placeholder tiebreak that sorts before every real one too.
    public async Task<ChangesPageDto> SinceTimeAsync(Guid projectId, DateTimeOffset since, int limit, Guid? mineUserId, CancellationToken cancellationToken = default)
    {
        CheckLimit(limit);
        var at = since.UtcDateTime;
        return await PageAsync(projectId, new ChangeCursor(at, 0, string.Empty), new ChangeCursor(at, 0, "-").Encode(), limit, mineUserId, cancellationToken);
    }

    private static void CheckLimit(int limit)
    {
        if (limit is < 1 or > MaxLimit)
        {
            throw ApiException.Validation($"The limit must be between 1 and {MaxLimit}.", "limit");
        }
    }

    private async Task<ChangesPageDto> PageAsync(Guid projectId, ChangeCursor? from, string? emptyCursor, int limit, Guid? mineUserId, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            projectId,
            mine = mineUserId,
            hasCursor = from is not null,
            cursorAt = from?.At ?? DateTime.UnixEpoch,
            cursorKind = from?.Kind ?? 0,
            cursorTie = from?.Tie ?? string.Empty,
            take = limit + 1,
        };
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<ChangeRow>(new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).ToList();
        var more = rows.Count > limit;
        if (more)
        {
            rows.RemoveAt(limit);
        }

        var entries = rows.Select(ToEntry).ToList();
        var next = rows.Count > 0 ? new ChangeCursor(DateTime.SpecifyKind(rows[^1].At, DateTimeKind.Utc), rows[^1].Kind, rows[^1].Tie).Encode() : emptyCursor;
        return new ChangesPageDto(entries, next, more);
    }

    private static ChangeEntryDto ToEntry(ChangeRow row) => new(
        row.WorkItemId,
        row.Key,
        row.Title,
        row.Kind == 0 ? ChangeKind.Revision : ChangeKind.Comment,
        row.ActorId,
        row.ActorName,
        new DateTimeOffset(DateTime.SpecifyKind(row.At, DateTimeKind.Utc)),
        row.Changes is null ? null : JsonSerializer.Deserialize<FieldChange[]>(row.Changes, PersistedJson.Options),
        row.Body,
        row.Revision);
}
