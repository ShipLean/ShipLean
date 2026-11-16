using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.ClickHouse;
using ShipLean.Application.Features.Inspection.ClickHouse.Schema;

namespace ShipLean.WebApi.Tests.Inspection.ClickHouse;

// A store that accepts the connection and never answers (a black hole) ends in the HTTP client's timeout. Every part reports it as
// unreachable instead of throwing, and start does not wait for it.
[TestFixture(TestName = "ClickHouse unreachable store, SHIP-1556")]
[Category("SHIP-1556")]
public sealed class ClickHouseUnreachableStoreTests
{
    [Test]
    public async Task Parts_WhenTheStoreNeverAnswers_ReportUnreachableAndStartReturnsAtOnce()
    {
        using var blackHole = new TcpListener(IPAddress.Loopback, 0);
        blackHole.Start();
        var options = new InspectionOptions
        {
            Enabled = true,
            Store = "clickhouse",
            ClickHouseConnection = $"Host=127.0.0.1;Port={((IPEndPoint)blackHole.LocalEndpoint).Port};Protocol=http;Username=u;Password=p;Database=otel;Timeout=1",
        };
        var migrator = new ClickHouseSchemaMigrator(options);
        var query = ScratchExporter.Query("SHIP-1");

        var watch = Stopwatch.StartNew();
        await migrator.StartAsync(CancellationToken.None);
        var startTook = watch.Elapsed;
        var search = await new ClickHouseRunFinder(options, migrator).FindAsync(query, CancellationToken.None);
        var health = await new ClickHouseHealth(options, migrator).CheckAsync(CancellationToken.None);
        var fields = await new ClickHouseFieldCheck(options, migrator).CheckAsync(CancellationToken.None);
        await migrator.StopAsync(CancellationToken.None);

        startTook.ShouldBeLessThan(TimeSpan.FromMilliseconds(500));
        search.Reachable.ShouldBeFalse();
        search.Problem.ShouldNotBeNull();
        health.Reachable.ShouldBeFalse();
        health.Problem.ShouldNotBeNull();
        fields.ShouldAllBe(field => !field.Found);
    }
}
