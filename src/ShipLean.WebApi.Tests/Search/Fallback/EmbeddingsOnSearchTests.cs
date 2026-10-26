using System.Text.Json;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Fallback;

// The default test host embeds queries (FakeEmbedder): the lexical leg stays AND. Rows here have no vectors, so only the lexical leg lists.
[TestFixture]
public class EmbeddingsOnSearchTests : ApiFixture
{
    [Test]
    public async Task Search_WhenEmbeddingsOn_ShouldStillRequireAllWords()
    {
        await using var connection = await OpenDatabaseAsync();
        var full = await FallbackItems.ArrangeAsync(connection, "Release notes", "allwords rollback checklist");
        await FallbackItems.ArrangeAsync(connection, "Staging notes", "allwords plan for staging");

        using var response = await Api.GetAsync("api/search?q=allwords%20rollback");

        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(text);
        JsonDocument.Parse(text).RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()).ShouldBe([full]);
    }
}
