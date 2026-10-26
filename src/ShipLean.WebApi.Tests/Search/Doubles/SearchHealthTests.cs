using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Search.Embedding;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Dtos;

namespace ShipLean.WebApi.Tests.Search.Doubles;

// /health "search" with the host's default embedder (the fake).
[TestFixture]
public class SearchHealthTests : ApiFixture
{
    [Test]
    public async Task Health_ShouldReportSearchMode()
    {
        var body = await Client.GetFromJsonAsync<HealthDto>("/health");

        body!.Search.ShouldBe(new HealthSearchDto("semantic", null, "fake-embedder@1"));
    }
}
