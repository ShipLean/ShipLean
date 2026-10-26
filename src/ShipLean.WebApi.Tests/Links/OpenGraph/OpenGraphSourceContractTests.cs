using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Application.Features.Links.OpenGraph;
using ShipLean.Application.Features.Links.PublicFetch;
using ShipLean.Application.Http;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Links;
using ShipLean.TestSupport.Contracts.Links;
using ShipLean.TestSupport.Http;

namespace ShipLean.WebApi.Tests.Links.OpenGraph;

// LinkCardSourceContract for the Open Graph source, wired by its own modules (no app host: the contract owns the stub server),
// with loopback let through so the stub can answer, and an in-memory blob store.
[TestFixture]
public class OpenGraphSourceContractTests : LinkCardSourceContract
{
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;

    [OneTimeSetUp]
    public void BuildServices()
    {
        var services = new ServiceCollection();
        new PublicFetchServices().AddServices(services, new ConfigurationBuilder().Build());
        new OpenGraphServices().AddServices(services, new ConfigurationBuilder().Build());
        services.AddScoped<PublicFetcher>();
        services.AddSingleton<IAddressCheck, LoopbackAllowedAddressCheck>();
        services.AddSingleton<IIntegrationSettings>(new FixedIntegrationSettings());
        services.AddSingleton<IBlobStore, InMemoryBlobStore>();
        _provider = (ServiceProvider)new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateScopes = true }).CreateServiceProvider(services);
    }

    [OneTimeTearDown]
    public async Task DisposeServices()
    {
        await _provider.DisposeAsync();
    }

    [SetUp]
    public void OpenScope()
    {
        _scope = _provider.CreateAsyncScope();
    }

    [TearDown]
    public async Task CloseScope()
    {
        await _scope.DisposeAsync();
    }

    protected override Uri ForeignAddress => new("ftp://files.example/readme");

    protected override ILinkCardSource CreateSource(StubHttpServer server) => _scope.ServiceProvider.GetRequiredService<ILinkCardSource>();

    protected override Uri HandledAddress(StubHttpServer server) => server.UrlOf("/articles/forms");

    protected override void ServeReadable(StubHttpServer server, Uri address)
    {
        PreviewPages.ServeArticle(server, address.AbsolutePath, "Payment and address form best practices");
    }

    protected override void ServeFailing(StubHttpServer server, Uri address)
    {
        server.Respond(address.AbsolutePath, "down", status: 503);
    }
}
