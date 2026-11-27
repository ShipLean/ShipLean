using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// SHIP-2338: a video is the one attachment type whose bytes go when its row is archived (sprint demos are re-delivered many times and
// live on YouTube); every other type keeps its blob. A video archived more than Grace ago has its blob deleted and every row on that
// storage_ref stamped blob_deleted_at; the rows stay. Grace lets a reader that is already streaming the file finish. A blob a live row
// still names is never deleted.
public sealed class ArchivedVideoSweeper(NpgsqlDataSource dataSource, IBlobStore blobs, TimeProvider clock, ILogger<ArchivedVideoSweeper> logger)
{
    public static readonly TimeSpan Grace = TimeSpan.FromHours(1);

    private const int BatchSize = 50;

    private sealed record Due(string StorageRef, long SizeBytes);

    // The number of blobs deleted.
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var due = (await connection.QueryAsync<Due>(new CommandDefinition(
            """
            SELECT a.storage_ref AS StorageRef, max(a.size_bytes) AS SizeBytes
            FROM attachment a
            WHERE a.archived_at IS NOT NULL AND a.archived_at < @before AND a.blob_deleted_at IS NULL AND a.content_type LIKE 'video/%'
              AND NOT EXISTS (SELECT 1 FROM attachment l WHERE l.storage_ref = a.storage_ref AND l.archived_at IS NULL)
            GROUP BY a.storage_ref
            ORDER BY min(a.archived_at)
            LIMIT @limit
            """,
            new { before = clock.GetUtcNow() - Grace, limit = BatchSize }, cancellationToken: cancellationToken))).ToList();

        foreach (var storageRef in due.Select(v => v.StorageRef))
        {
            await blobs.DeleteAsync(storageRef, cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE attachment SET blob_deleted_at = @now WHERE storage_ref = @storageRef AND blob_deleted_at IS NULL",
                new { now = clock.GetUtcNow(), storageRef }, cancellationToken: cancellationToken));
        }

        if (due.Count > 0)
        {
            logger.LogInformation("Deleted {Count} archived video files, {Bytes} bytes", due.Count, due.Sum(v => v.SizeBytes));
        }

        return due.Count;
    }
}
