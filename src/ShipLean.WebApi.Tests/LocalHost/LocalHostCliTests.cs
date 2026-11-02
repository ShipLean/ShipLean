using System.Net;
using System.Net.Sockets;
using ShipLean.LocalEnvironment.Database;
using ShipLean.LocalHost;

namespace ShipLean.WebApi.Tests.LocalHost;

[TestFixture]
public class LocalHostCliTests
{
    private readonly string _database = "shiplean_lh_" + Guid.NewGuid().ToString("N")[..8];

    [TearDown]
    public Task DropDatabase() => ShipLeanDatabase.DropNamedAsync(_database);

    [Test]
    public async Task RunAsync_WhenArgumentsAreBad_ShouldReturnTwoAndPrintUsage()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await LocalHostCli.RunAsync(["--database", _database, "--bogus"], output, error);

        code.ShouldBe(2);
        error.ToString().ShouldContain("--bogus");
        error.ToString().ShouldContain("Usage: dotnet run --project src/ShipLean.LocalHost");
        output.ToString().ShouldBeEmpty();
    }

    [Test]
    public async Task RunAsync_WhenPortIsInUse_ShouldReturnOne()
    {
        using var listener = new TcpListener(IPAddress.IPv6Any, 0);
        listener.Server.DualMode = true;
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var output = new StringWriter();
        var error = new StringWriter();

        var code = await LocalHostCli.RunAsync(["--port", port.ToString(), "--database", _database], output, error);

        code.ShouldBe(1);
        error.ToString().ShouldNotBeEmpty();
        output.ToString().ShouldNotContain("ShipLean is running");
    }
}
