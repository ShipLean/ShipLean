using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Search;
using ShipLean.Application.Features.Search.Embedding;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Dtos;

namespace ShipLean.WebApi.Tests.Search.Embedding;

// A model folder that is not there: search is lexical with a reason, and the host still serves.
[TestFixture]
public class HealthModelMissingTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => EmbedderServices.Register;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        [SearchOptions.ModelPathKey] = "/nonexistent/embedding-model",
    };

    [Test]
    public async Task Health_WhenModelMissing_ShouldReportLexicalAndKeepServing()
    {
        var body = await Client.GetFromJsonAsync<HealthDto>("/health");

        var search = body.ShouldNotBeNull().Search.ShouldNotBeNull();
        search.Mode.ShouldBe("lexical");
        (search.Reason ?? string.Empty).ShouldContain("/nonexistent/embedding-model");
        search.Model.ShouldBeNull();

        using var response = await Api.GetAsync("api/search?q=x");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonDocument.Parse(text).RootElement.GetProperty("semantic").GetBoolean().ShouldBeFalse();
    }
}
