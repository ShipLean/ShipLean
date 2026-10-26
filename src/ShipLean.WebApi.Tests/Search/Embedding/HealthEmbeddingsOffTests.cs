using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Search;
using ShipLean.Application.Features.Search.Embedding;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Dtos;

namespace ShipLean.WebApi.Tests.Search.Embedding;

// The production factory with SHIPLEAN_EMBEDDINGS=off, even though a model path is set.
[TestFixture]
public class HealthEmbeddingsOffTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => EmbedderServices.Register;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        [SearchOptions.EmbeddingsKey] = "off",
        [SearchOptions.ModelPathKey] = "/nonexistent",
    };

    [Test]
    public async Task Health_WhenEmbeddingsOff_ShouldReportLexical()
    {
        var body = await Client.GetFromJsonAsync<HealthDto>("/health");

        body!.Search.ShouldBe(new HealthSearchDto("lexical", "Embeddings are switched off (SHIPLEAN_EMBEDDINGS=off).", null));
    }
}
