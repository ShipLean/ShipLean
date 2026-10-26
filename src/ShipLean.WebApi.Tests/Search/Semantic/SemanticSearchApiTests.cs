using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Search;

namespace ShipLean.WebApi.Tests.Search.Semantic;

// GET /api/search with the real ranker and the semantic leg over rows embedded by the fake.
[TestFixture]
public class SemanticSearchApiTests : SemanticFixture
{
    [Test]
    public async Task Search_WhenParaphrase_ShouldFindBySemantics()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await ItemAsync(connection, "Sign-in fails for SSO users");
        await RowAsync(connection, item, SearchField.Title, "Sign-in fails for SSO users");
        var key = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @item", new { item });

        using var response = await Api.GetAsync("api/search?q=login%20broken");

        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(text);
        var root = JsonDocument.Parse(text).RootElement;
        root.GetProperty("semantic").GetBoolean().ShouldBeTrue();
        root.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()).ShouldContain(key);
    }
}
