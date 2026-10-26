using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Links.PublicFetch;
using ShipLean.Application.Http;
using ShipLean.Contracts.Links;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Http;
using Shouldly;

namespace ShipLean.WebApi.Tests.Links.OpenGraph;

// The one exception to the public-address rule: an address PreviewAllowHosts allows. The real PublicAddressCheck is in place
// (loopback refused). "intranet.test", "portless.test" and "other.test" all resolve to the stub server on loopback.
// "Intranet.test:{stub port}" is allow-listed; so is "portless.test", which allows only port 80/443, never the stub's port.
[TestFixture]
public class PublicFetcherAllowHostTests : EmptyApiFixture
{
    private readonly FixedIntegrationSettings _settings = new();

    private StubHttpServer _server = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IHostResolver>(new FixedHostResolver(new Dictionary<string, string>
        {
            ["intranet.test"] = "127.0.0.1",
            ["other.test"] = "127.0.0.1",
            ["portless.test"] = "127.0.0.1",
        }));
        services.AddSingleton<IIntegrationSettings>(_settings);
    };

    [OneTimeSetUp]
    public async Task StartStubServer()
    {
        _server = await StubHttpServer.StartAsync();
        _settings.PreviewAllowHosts = [$"Intranet.test:{_server.BaseAddress.Port}", "portless.test"];
        _server.Respond("/wiki", "<title>Wiki</title>");
        _server.Respond("/to-other", string.Empty, status: 302, headers: new Dictionary<string, string> { ["Location"] = $"http://other.test:{_server.BaseAddress.Port}/wiki" });
    }

    [OneTimeTearDown]
    public async Task StopStubServer()
    {
        await _server.DisposeAsync();
    }

    private async Task<PublicFetchResult> FetchAsync(string host, string path)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PublicFetcher>()
            .FetchAsync(new Uri($"http://{host}:{_server.BaseAddress.Port}{path}"), "text/html", CancellationToken.None);
    }

    [Test]
    public async Task Fetch_WhenHostAndPortAreOnPreviewAllowHosts_ShouldReachAPrivateAddress()
    {
        var result = await FetchAsync("intranet.test", "/wiki");

        result.Status.ShouldBe(PublicFetchStatus.Ok);
    }

    [Test]
    public async Task Fetch_WhenHostIsNotOnPreviewAllowHosts_ShouldRefuseThePrivateAddress()
    {
        var result = await FetchAsync("other.test", "/wiki");

        result.Status.ShouldBe(PublicFetchStatus.Private);
    }

    [Test]
    public async Task Fetch_WhenAnAllowedHostRedirectsToAnotherHost_ShouldCheckTheOtherHost()
    {
        var result = await FetchAsync("intranet.test", "/to-other");

        result.Status.ShouldBe(PublicFetchStatus.Private);
    }

    [Test]
    public async Task Fetch_WhenTheEntryNamesNoPortAndThePortIsNotTheDefault_ShouldRefuseThePrivateAddress()
    {
        var result = await FetchAsync("portless.test", "/wiki");

        result.Status.ShouldBe(PublicFetchStatus.Private);
    }
}
