using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Search.Embedding;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Dtos;

namespace ShipLean.WebApi.Tests.Search.Doubles;

// The same with a lexical embedder, whose reason is reported.
[TestFixture]
public class SearchHealthLexicalTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices
        => services => services.Replace(ServiceDescriptor.Singleton<IEmbedder>(new NoEmbedder("Embeddings are switched off.")));

    [Test]
    public async Task Health_WhenEmbedderLexical_ShouldReportReason()
    {
        var body = await Client.GetFromJsonAsync<HealthDto>("/api/health");

        body!.Search.ShouldBe(new HealthSearchDto("lexical", "Embeddings are switched off.", null));
    }
}
