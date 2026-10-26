using Dapper;
using Npgsql;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;

namespace ShipLean.WebApi.Tests.Search.Fallback;

// Arranges an item with a title row and a body row directly, no indexer. The body row's first line is the title, as the writer makes it.
internal static class FallbackItems
{
    public static async Task<string> ArrangeAsync(NpgsqlConnection connection, string title, string body)
    {
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle(title).WithType("story").InsertAsync(connection);
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Item, id, SearchField.Title, title).WithWorkItem(id).InsertAsync(connection);
        await new SearchDocBuilder(SeedIds.Projects.Ship, SearchSourceKind.Item, id, SearchField.Body, title + "\n" + body).WithWorkItem(id).InsertAsync(connection);
        return (await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id }))!;
    }
}
