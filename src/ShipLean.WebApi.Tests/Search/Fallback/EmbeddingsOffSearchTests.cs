using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Search.Embedding;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Fallback;

// The real legs with the embedder off: the lexical leg fuses an AND list and an any-word list.
[TestFixture]
public class EmbeddingsOffSearchTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.Replace(ServiceDescriptor.Singleton<IEmbedder>(new NoEmbedder("Embeddings are switched off.")));

    [Test]
    public async Task Search_WhenEmbeddingsOff_ShouldMatchAnyWord()
    {
        await using var connection = await OpenDatabaseAsync();
        var full = await FallbackItems.ArrangeAsync(connection, "Release notes", "anyword rollback checklist");
        var partial = await FallbackItems.ArrangeAsync(connection, "Staging notes", "anyword plan for staging");
        var unrelated = await FallbackItems.ArrangeAsync(connection, "Garden notes", "unrelated gardening");

        var keys = await KeysAsync("q=anyword%20rollback");

        keys.ShouldBe([full, partial]);
        keys.ShouldNotContain(unrelated);
    }

    [Test]
    public async Task Search_WhenEmbeddingsOffAndPrefixTyped_ShouldMatchAnyWordWithTheLastAsPrefix()
    {
        await using var connection = await OpenDatabaseAsync();
        var full = await FallbackItems.ArrangeAsync(connection, "Release notes", "prefixany rollback checklist");
        var partial = await FallbackItems.ArrangeAsync(connection, "Staging notes", "prefixany plan for staging");

        // The seed data has its own "rollb…" items, so assert presence and order of ours only.
        var keys = await KeysAsync("q=prefixany%20rollb&prefix=true");

        keys.ShouldContain(full);
        keys.ShouldContain(partial);
        keys.IndexOf(full).ShouldBeLessThan(keys.IndexOf(partial));
    }

    [Test]
    public async Task Search_WhenEmbeddingsOffAndQuoted_ShouldKeepTheExactPhrase()
    {
        await using var connection = await OpenDatabaseAsync();
        var phrase = await FallbackItems.ArrangeAsync(connection, "Release notes", "quotedany rollback checklist");
        await FallbackItems.ArrangeAsync(connection, "Staging notes", "quotedany plan for staging");

        (await KeysAsync("q=%22quotedany%20rollback%22")).ShouldBe([phrase]);
    }

    private async Task<List<string>> KeysAsync(string query)
    {
        using var response = await Api.GetAsync($"api/search?{query}");
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.ShouldBeTrue(text);
        return [.. JsonDocument.Parse(text).RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()!)];
    }
}
