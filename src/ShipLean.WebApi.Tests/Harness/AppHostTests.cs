using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Application.Features.Holidays;
using ShipLean.Application.Features.Search.Index;
using ShipLean.Application.Identity;
using ShipLean.LocalEnvironment.Database;
using ShipLean.LocalEnvironment.Hosting;

namespace ShipLean.WebApi.Tests.Harness;

[TestFixture]
public class AppHostTests
{
    private ClonedDatabase _database = null!;

    [OneTimeSetUp]
    public async Task CloneDatabase() => _database = await ShipLeanDatabase.CloneSeedAsync();

    [OneTimeTearDown]
    public async Task DropDatabase() => await _database.DisposeAsync();

    [Test]
    public async Task Start_WhenPortIsGiven_ShouldListenOnThatPort()
    {
        var port = FreePort();

        await using var host = AppHost.Start(new AppHostOptions { ConnectionString = _database.ConnectionString, Port = port });

        host.BaseUrl.ShouldBe($"http://127.0.0.1:{port}");
        using var client = host.CreateClient();
        (await client.GetAsync("/health")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Start_WhenPortIsZero_ShouldPickAFreePort()
    {
        await using var host = AppHost.Start(new AppHostOptions { ConnectionString = _database.ConnectionString });

        new Uri(host.BaseUrl).Port.ShouldBeGreaterThan(0);
        using var client = host.CreateClient();
        (await client.GetAsync("/health")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Start_WhenTestProfile_ShouldRemoveTheIndexerAndOutboundServices()
    {
        await using var host = AppHost.Start(new AppHostOptions { ConnectionString = _database.ConnectionString, Profile = HostProfile.Test });

        var hosted = HostedServiceTypes(host);

        hosted.ShouldNotContain(typeof(SearchIndexHostedService));
        hosted.ShouldNotContain(typeof(HolidayRefreshService));
        hosted.ShouldNotContain(typeof(AgentDeliveryService));
        hosted.ShouldNotContain(typeof(AgentStaleService));
    }

    [Test]
    public async Task Start_WhenLocalProfile_ShouldKeepTheIndexerAndRemoveOutboundServices()
    {
        await using var host = AppHost.Start(new AppHostOptions { ConnectionString = _database.ConnectionString, Profile = HostProfile.Local });

        var hosted = HostedServiceTypes(host);

        hosted.ShouldContain(typeof(SearchIndexHostedService));
        hosted.ShouldNotContain(typeof(HolidayRefreshService));
        hosted.ShouldNotContain(typeof(AgentDeliveryService));
        hosted.ShouldNotContain(typeof(AgentStaleService));
    }

    [Test]
    public async Task Start_WhenSettingsGiven_ShouldApplyThemLast()
    {
        // AppHost blanks the first-admin email; a setting from the caller wins over that.
        var settings = new Dictionary<string, string?> { [AdminBootstrap.EmailKey] = "settings-win@shiplean.local" };

        await using var host = AppHost.Start(new AppHostOptions { ConnectionString = _database.ConnectionString, Settings = settings });

        host.Services.GetRequiredService<IConfiguration>()[AdminBootstrap.EmailKey].ShouldBe("settings-win@shiplean.local");
    }

    [Test]
    public async Task Stopping_WhenHostIsDisposed_ShouldBeCancelled()
    {
        var host = AppHost.Start(new AppHostOptions { ConnectionString = _database.ConnectionString });
        var stopping = host.Stopping;
        stopping.IsCancellationRequested.ShouldBeFalse();

        await host.DisposeAsync();

        stopping.IsCancellationRequested.ShouldBeTrue();
    }

    private static IReadOnlyList<Type> HostedServiceTypes(AppHost host)
        => host.Services.GetServices<IHostedService>().Select(service => service.GetType()).ToList();

    // Free a moment ago: the listener takes a port from the OS and gives it back before the app binds it.
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
