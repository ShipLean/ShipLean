using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Links;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using Shouldly;

namespace ShipLean.WebApi.Tests.Links.GitHub;

// The GitHub source's HTTP client resolves every name through the registered IHostResolver: with the host's default
// (no-network) resolver a name that isn't an address can't be reached, so a real GitHubApiUrl never gets a connection in tests.
[TestFixture]
public class GitHubNameResolutionTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.AddScoped<IIntegrationSettings>(_ => new FixedIntegrationSettings(new IntegrationSettingsDto(null, "https://api.github.example.test", null, null, [])));

    [Test]
    public async Task Add_WhenApiHostNameCannotBeResolved_ShouldFileUnreachableCardWithNumberAndLocation()
    {
        using var response = await Api.PostJsonAsync($"api/work-items/{SeedIds.WorkItems.Ship101}/link-cards", new { url = $"https://github.com/acme/storefront/pull/{Random.Shared.Next(1000, 900000)}" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var card = (await response.Content.ReadFromJsonAsync<LinkCardDto>(Wire))!;
        card.PreviewStatus.ShouldBe(LinkPreviewStatus.Unreachable);
        card.Kind.ShouldBe(LinkKind.GithubPull);
        card.Number.ShouldNotBeNull();
        card.Location.ShouldBe("acme/storefront");
    }
}
