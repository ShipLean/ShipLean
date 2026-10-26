using System.Globalization;
using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Criteria;

// Inserts a SHIP work item whose columns hold a case's field values (a field the case does not list keeps the column's default),
// and returns its id. The case's ids are seed ids, so the parent, state and type are looked up from the seed rather than assumed.
internal static class CriteriaWorkItemRow
{
    private static int _numbers = 100_000;

    public static async Task<Guid> InsertAsync(NpgsqlDataSource dataSource, JsonElement item)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        var number = Interlocked.Increment(ref _numbers);
        var id = GuidOf(item, "id") ?? Guid.NewGuid();
        var parentId = GuidOf(item, "parent") ?? GuidOf(item, "epic");
        var parentType = parentId is null ? null : await connection.QuerySingleOrDefaultAsync<string>("SELECT type FROM work_item WHERE id = @parentId", new { parentId });
        var stateId = GuidOf(item, "state");
        var type = Text(item, "type")
            ?? (stateId is null ? null : await connection.QuerySingleOrDefaultAsync<string>("SELECT type FROM state WHERE id = @stateId", new { stateId }))
            ?? parentType switch { "epic" => "feature", "feature" => "story", "story" or "bug" => "task", _ => "story" };
        stateId ??= await connection.QuerySingleAsync<Guid>(
            "SELECT id FROM state WHERE project_id = @project AND type = @type AND category = @category AND archived_at IS NULL ORDER BY sort_order LIMIT 1",
            new { project = SeedIds.Projects.Ship, type, category = Text(item, "stateCategory") ?? "proposed" });
        var key = Text(item, "key") ?? $"SHIP-{number}";
        var originalSystem = Text(item, "originalSystem");
        var originalId = Text(item, "originalSystemId") ?? (originalSystem is null ? null : $"ORIG-{number}");

        // Seed rows and earlier cases may already hold the key or the original id; they are renamed, not deleted.
        await connection.ExecuteAsync("UPDATE work_item SET key = 'ZZ-' || number WHERE key = @key", new { key });
        if (originalId is not null)
        {
            await connection.ExecuteAsync(
                "UPDATE work_item SET original_system = NULL, original_system_id = NULL, original_url = NULL WHERE project_id = @project AND original_system_id = @originalId",
                new { project = SeedIds.Projects.Ship, originalId });
        }

        await connection.ExecuteAsync(
            """
            INSERT INTO work_item (id, project_id, number, key, type, title, state_id, assigned_to, sprint_id, priority, story_points, backlog_order,
                                   parent_id, parent_type, tags, severity, original_system, original_system_id, created_by, created_at, changed_by, changed_at)
            VALUES (@id, @project, @number, @key, @type, @title, @stateId, @assignee, @sprint, @priority, @points, @order,
                    @parentId, @parentType, @tags, @severity, @originalSystem, @originalId, @createdBy, @createdAt, @changedBy, @changedAt)
            ON CONFLICT (id) DO NOTHING
            """,
            new
            {
                id, project = SeedIds.Projects.Ship, number, key, type, title = Text(item, "title") ?? "Arranged work item", stateId,
                assignee = GuidOf(item, "assignee"), sprint = GuidOf(item, "sprint"), priority = (short)(Number(item, "priority") ?? 2),
                points = Number(item, "points"), order = $"zz{number}", parentId, parentType,
                tags = Tags(item), severity = (short?)Number(item, "severity"),
                originalSystem = originalId is null ? null : originalSystem ?? "jira", originalId,
                createdBy = GuidOf(item, "createdBy"), createdAt = Instant(item, "createdAt") ?? TimeProvider.System.GetUtcNow(),
                changedBy = GuidOf(item, "changedBy"), changedAt = Instant(item, "changedAt") ?? TimeProvider.System.GetUtcNow(),
            });
        return id;
    }

    private static Guid? GuidOf(JsonElement item, string field)
        => Text(item, field) is { } text ? Guid.Parse(text) : null;

    private static string? Text(JsonElement item, string field)
        => item.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static decimal? Number(JsonElement item, string field)
        => item.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : null;

    private static string[] Tags(JsonElement item)
        => item.TryGetProperty("tags", out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(t => t.GetString()!).ToArray() : [];

    private static DateTimeOffset? Instant(JsonElement item, string field)
        => Text(item, field) is { } text ? DateTimeOffset.Parse(text, CultureInfo.InvariantCulture).ToUniversalTime() : null;
}
