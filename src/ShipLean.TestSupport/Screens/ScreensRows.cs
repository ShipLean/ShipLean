using Dapper;
using Npgsql;

namespace ShipLean.TestSupport.Screens;

// Reads the phone screens' stored rows back raw (N1), for tests that assert what a writer stored: a work item's health columns
// (health as the stored string), a retro point's supports and a digest-seen row. Times are UTC.
public static class ScreensRows
{
    public sealed record Health(string? Value, DateTimeOffset? SetAt, Guid? SetBy, string? SetAgent);

    public sealed record Support(Guid Id, Guid PointId, Guid UserId, DateTimeOffset CreatedAt, DateTimeOffset? WithdrawnAt);

    public static async Task<Health> HealthAsync(NpgsqlConnection connection, Guid itemId) =>
        await connection.QuerySingleAsync<Health>(
            """
            SELECT health AS Value, health_set_at AS SetAt, health_set_by AS SetBy, health_set_agent AS SetAgent
            FROM work_item WHERE id = @itemId
            """,
            new { itemId });

    // Every support on the point, live and withdrawn, oldest first.
    public static async Task<IReadOnlyList<Support>> SupportAsync(NpgsqlConnection connection, Guid pointId) =>
        (await connection.QueryAsync<Support>(
            """
            SELECT id AS Id, point_id AS PointId, user_id AS UserId, created_at AS CreatedAt, withdrawn_at AS WithdrawnAt
            FROM retro_point_support WHERE point_id = @pointId ORDER BY created_at, id
            """,
            new { pointId })).ToList();

    // When the person last saw the project's Feature digest, or null when they never have.
    public static async Task<DateTimeOffset?> DigestSeenAsync(NpgsqlConnection connection, Guid userId, Guid projectId) =>
        await connection.QuerySingleOrDefaultAsync<DateTimeOffset?>(
            "SELECT seen_at FROM feature_digest_seen WHERE user_id = @userId AND project_id = @projectId", new { userId, projectId });
}
