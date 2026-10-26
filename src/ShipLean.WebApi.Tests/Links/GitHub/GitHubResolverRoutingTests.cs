using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Http;
using ShipLean.Contracts.Links;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Http;
using Shouldly;

namespace ShipLean.WebApi.Tests.Links.GitHub;

// The other half of GitHubNameResolutionTests: when the registered IHostResolver does know the API host's name, the
// source reaches the stub through exactly that lookup (and the address check), so nothing else resolves names.
[TestFixture]
public class GitHubResolverRoutingTests : ApiFixture
{
    private const string ApiName = "api.github.example.test";

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private readonly NamedLoopbackResolver _resolver = new(ApiName);
    private StubHttpServer _github = null!;

    protected override async Task<ClonedDatabase> CloneDatabaseAsync()
    {
        _github = await StubHttpServer.StartAsync();
        _github.Respond("/repos/acme/storefront/pulls/9482", """{ "number": 9482, "title": "Add retry to checkout", "state": "open", "draft": false, "merged": false, "user": { "login": "mira" } }""", "application/json");
        return await base.CloneDatabaseAsync();
    }

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IHostResolver>(_resolver);
        services.AddSingleton<IAddressCheck>(new AllowLoopback());
        services.AddScoped<IIntegrationSettings>(_ => new FixedIntegrationSettings(new IntegrationSettingsDto(null, $"http://{ApiName}:{_github.BaseAddress.Port}", null, null, [])));
    };

    [OneTimeTearDown]
    public async Task StopGitHub() => await _github.DisposeAsync();

    [Test]
    public async Task Add_WhenResolverKnowsTheApiHostName_ShouldReadThePullThroughIt()
    {
        using var response = await Api.PostJsonAsync($"api/work-items/{SeedIds.WorkItems.Ship101}/link-cards", new { url = "https://github.com/acme/storefront/pull/9482" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var card = (await response.Content.ReadFromJsonAsync<LinkCardDto>(Wire))!;
        card.Title.ShouldBe("Add retry to checkout");
        card.State.ShouldBe(LinkState.Open);
        _resolver.Lookups.ShouldContain(ApiName);
    }

    private sealed class NamedLoopbackResolver(string name) : IHostResolver
    {
        private readonly List<string> _lookups = [];

        public IReadOnlyList<string> Lookups => _lookups;

        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            _lookups.Add(host);
            return Task.FromResult<IReadOnlyList<IPAddress>>(host == name ? [IPAddress.Loopback] : []);
        }
    }

    private sealed class AllowLoopback : IAddressCheck
    {
        public bool IsAllowed(IPAddress address) => IPAddress.IsLoopback(address);
    }
}
