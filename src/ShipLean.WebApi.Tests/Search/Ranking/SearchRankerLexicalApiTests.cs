using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Search.Embedding;
using ShipLean.Application.Features.Search.Ranking;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Search.Endpoint;

namespace ShipLean.WebApi.Tests.Search.Ranking;

// The same ranker with embeddings off: the lexical leg alone answers and nothing embeds the query.
[TestFixture]
public class SearchRankerLexicalApiTests : ApiFixture
{
    private readonly ScriptedItemCandidates _lexical = new(CandidateLeg.Lexical);
    private readonly ScriptedItemCandidates _semantic = new(CandidateLeg.Semantic) { Available = false };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IItemCandidates>();
        services.AddSingleton<IItemCandidates>(_lexical);
        services.AddSingleton<IItemCandidates>(_semantic);
        services.Replace(ServiceDescriptor.Singleton<ISearchGroups>(new StubSearchGroups()));
        services.Replace(ServiceDescriptor.Singleton<IEmbedder>(new NoEmbedder("Embeddings are switched off.")));
    };

    [Test]
    public async Task Search_WhenEmbeddingsOff_ShouldFallBackToLexical()
    {
        await using var connection = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Lexical only").InsertAsync(connection);
        _lexical.Lists = [new RankedList(RankedListName.TitleLexical, [StubItemCandidates.Hit(item, "lexical snippet")])];

        using var response = await Api.GetAsync("api/search?q=lexical");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var root = JsonDocument.Parse(text).RootElement;
        root.GetProperty("semantic").GetBoolean().ShouldBeFalse();
        root.GetProperty("items")[0].GetProperty("snippet").GetString().ShouldBe("lexical snippet");
    }
}
