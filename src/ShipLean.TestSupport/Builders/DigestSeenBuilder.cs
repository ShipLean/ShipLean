using Dapper;
using Npgsql;

namespace ShipLean.TestSupport.Builders;

// Arranges when a person last saw a project's Feature digest (feature_digest_seen). The row is keyed (user_id, project_id), so
// InsertAsync upserts: a second insert for the same pair moves seen_at. The default is now.
public sealed class DigestSeenBuilder(Guid userId, Guid projectId)
{
    private DateTimeOffset? _seenAt;

    public DigestSeenBuilder WithSeenAt(DateTimeOffset seenAt)
    {
        _seenAt = seenAt;
        return this;
    }

    public async Task InsertAsync(NpgsqlConnection connection) =>
        await connection.ExecuteAsync(
            """
            INSERT INTO feature_digest_seen (user_id, project_id, seen_at)
            VALUES (@userId, @projectId, coalesce(@seenAt::timestamptz, now()))
            ON CONFLICT (user_id, project_id) DO UPDATE SET seen_at = excluded.seen_at
            """,
            new { userId, projectId, seenAt = _seenAt?.ToUniversalTime() });
}
