using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Search;
using ShipLean.Application.Features.Search.Embedding;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Dtos;

namespace ShipLean.WebApi.Tests.Search.Embedding;

// The production factory with the real model.
// Runs alone: the real model is ~700 MB resident while loaded, and CI's 2 GB runners can't hold several next to the parallel hosts.
[TestFixture]
[NonParallelizable]
public class HealthModelLoadedTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => EmbedderServices.Register;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        [SearchOptions.ModelPathKey] = RealModel.Directory,
    };

    [Test]
    public async Task Health_WhenModelLoaded_ShouldReportSemanticWithModelId()
    {
        var body = await Client.GetFromJsonAsync<HealthDto>("/health");

        body!.Search.ShouldBe(new HealthSearchDto("semantic", null, "intfloat/multilingual-e5-small@614241f622f53c4eeff9890bdc4f31cfecc418b3"));
    }
}
