using ShipLean.Application.Features.Inspection.ClickHouse;
using ShipLean.Application.Features.Inspection.ClickHouse.Schema;

namespace ShipLean.WebApi.Tests.Inspection.ClickHouse;

// The views read columns of the exporter's tables; when one is missing Inspection says which, rather than failing in a query.
[TestFixture(TestName = "ClickHouse exporter layout, SHIP-1556")]
[Category("SHIP-1556")]
public sealed class ClickHouseExporterLayoutTests
{
    [Test]
    public async Task Ensure_WhenTheTracesTableLacksAColumnTheViewsRead_FailsNamingTheTableAndTheColumn()
    {
        await using var scratch = await ScratchExporter.CreateAsync(
            "Timestamp DateTime64(9), SpanName String, ServiceName String, ResourceAttributes Map(String, String), Duration UInt64, StatusCode String");

        var failure = await Should.ThrowAsync<ClickHouseLayoutException>(() => scratch.Migrator().EnsureAsync(CancellationToken.None));

        failure.Message.ShouldContain($"{scratch.ExporterDatabase}.otel_traces");
        failure.Message.ShouldContain("SpanAttributes");
    }

    [Test]
    public async Task Ensure_WhenTheLogsTableLacksAColumnTheReaderReads_FailsNamingTheTableAndTheColumn()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        await scratch.Db.ExecuteAsync($"ALTER TABLE {scratch.ExporterDatabase}.otel_logs DROP COLUMN Body", null, CancellationToken.None);

        var failure = await Should.ThrowAsync<ClickHouseLayoutException>(() => scratch.Migrator().EnsureAsync(CancellationToken.None));

        failure.Message.ShouldContain($"{scratch.ExporterDatabase}.otel_logs");
        failure.Message.ShouldContain("Body");
    }

    [Test]
    public async Task Health_WhenAColumnTheReaderReadsIsMissing_ReportsAProblemNamingIt()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        await scratch.Db.ExecuteAsync($"ALTER TABLE {scratch.ExporterDatabase}.otel_traces DROP COLUMN StatusMessage", null, CancellationToken.None);

        var health = await new ClickHouseHealth(scratch.Options, scratch.Migrator()).CheckAsync(CancellationToken.None);

        health.Reachable.ShouldBeFalse();
        health.Problem.ShouldNotBeNull().ShouldContain("StatusMessage");
    }

    [Test]
    public async Task Ensure_WhenTheTablesHaveEveryColumn_Succeeds()
    {
        await using var scratch = await ScratchExporter.CreateAsync();

        (await scratch.Migrator().EnsureAsync(CancellationToken.None)).ShouldBeTrue();
    }
}
