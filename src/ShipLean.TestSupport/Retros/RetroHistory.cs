using Dapper;
using Npgsql;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport.Builders;

namespace ShipLean.TestSupport.Retros;

// Arranges state-change and sprint-change revisions the way the work item store records them and the metrics read them
// ("completed at T", "moved to sprint S at T"), keeping work_item in step. The item exists already (WorkItemBuilder).
public static class RetroHistory
{
    // Inserts the item and dates its creation revision long ago, as the store's history is read: a later change always sorts after it.
    public static async Task<Guid> CreateAsync(NpgsqlConnection connection, WorkItemBuilder builder)
    {
        var id = await builder.InsertAsync(connection);
        await connection.ExecuteAsync("UPDATE revision SET changed_at = @created WHERE work_item_id = @id AND rev = 1", new { id, created = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero) });
        return id;
    }

    public static async Task MoveToSprintAsync(NpgsqlConnection connection, Guid itemId, Guid? sprintId, DateTimeOffset at)
    {
        var from = await connection.ExecuteScalarAsync<Guid?>("SELECT sprint_id FROM work_item WHERE id = @itemId", new { itemId });
        await AppendAsync(connection, itemId, at, FieldIds.Sprint, from, sprintId);
        await connection.ExecuteAsync("UPDATE work_item SET sprint_id = @sprintId WHERE id = @itemId", new { itemId, sprintId });
    }

    // Into the project's first state of the item's type in the completed category.
    public static Task CompleteAsync(NpgsqlConnection connection, Guid itemId, DateTimeOffset at) => EnterAsync(connection, itemId, at, "completed");

    public static Task ReopenAsync(NpgsqlConnection connection, Guid itemId, DateTimeOffset at) => EnterAsync(connection, itemId, at, "inProgress");

    private static async Task EnterAsync(NpgsqlConnection connection, Guid itemId, DateTimeOffset at, string category)
    {
        var item = await connection.QuerySingleAsync<(Guid State, Guid Target)>(
            """
            SELECT w.state_id AS State, (SELECT s.id FROM state s WHERE s.project_id = w.project_id AND s.type = w.type AND s.category = @category AND s.archived_at IS NULL ORDER BY s.sort_order LIMIT 1) AS Target
            FROM work_item w WHERE w.id = @itemId
            """,
            new { itemId, category });
        await AppendAsync(connection, itemId, at, FieldIds.State, item.State, item.Target);
        await connection.ExecuteAsync("UPDATE work_item SET state_id = @state WHERE id = @itemId", new { itemId, state = item.Target });
    }

    private static async Task AppendAsync(NpgsqlConnection connection, Guid itemId, DateTimeOffset at, string field, Guid? from, Guid? to)
    {
        var rev = await connection.ExecuteScalarAsync<int>("SELECT max(rev) + 1 FROM revision WHERE work_item_id = @itemId", new { itemId });
        await new RevisionBuilder(itemId).WithRev(rev).WithChangedAt(at)
            .WithChanges($$"""[{"field":"{{field}}","from":{{Json(from)}},"to":{{Json(to)}},"fromLabel":null,"toLabel":null}]""")
            .InsertAsync(connection);
    }

    private static string Json(Guid? id) => id is null ? "null" : $"\"{id}\"";
}
