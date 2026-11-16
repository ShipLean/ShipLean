using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Inspection.TempoLoki;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

// PostgresRunLinkStore against a cloned test database: upsert on the primary key, rows older than the cutoff are ignored, nothing is deleted.
[TestFixture(TestName = "Postgres run-link store")]
public sealed class PostgresRunLinkStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 9, 0, 0, TimeSpan.Zero);
    private PostgresWorkItemStoreArrangement _arrangement = null!;
    private NpgsqlDataSource _dataSource = null!;
    private PostgresRunLinkStore _store = null!;

    [OneTimeSetUp]
    public async Task CloneDatabase()
    {
        DapperTypeHandlers.Register();
        _arrangement = await PostgresWorkItemStoreArrangement.CreateAsync();
        _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(_arrangement.Database.ConnectionString));
        _store = new PostgresRunLinkStore(_dataSource);
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        await _dataSource.DisposeAsync();
        await _arrangement.DisposeAsync();
    }

    private static RunLinkRow Row(string run, DateTimeOffset fetched, string source = "Tagged", params string[] traces)
        => new(run, "session-" + run, traces, source, "{\"runId\":\"" + run + "\"}", fetched);

    private async Task<long> CountAsync(string project)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM inspection_run_links WHERE project_key = @project", new { project });
    }

    [Test]
    public async Task Write_WhenTheRunIsWrittenAgain_ReplacesTheRowAndKeepsOne()
    {
        await _store.WriteAsync("UP", "UP-1", [Row("r1", Now.AddMinutes(-5), "Tagged", "t1")], CancellationToken.None);
        await _store.WriteAsync("UP", "UP-1", [Row("r1", Now, "FromToolCall", "t1", "t2")], CancellationToken.None);

        var rows = await _store.ReadAsync("UP", "UP-1", Now.AddMinutes(-10), CancellationToken.None);

        rows.Count.ShouldBe(1);
        rows[0].Source.ShouldBe("FromToolCall");
        rows[0].TraceIds.ShouldBe(["t1", "t2"]);
        rows[0].FetchedAt.ShouldBe(Now);
        rows[0].Summary.ShouldContain("r1");
    }

    [Test]
    public async Task Read_WhenARowIsOlderThanTheCutoff_IgnoresIt()
    {
        await _store.WriteAsync("OLD", "OLD-1", [Row("fresh", Now.AddMinutes(-9)), Row("stale", Now.AddMinutes(-11))], CancellationToken.None);

        var rows = await _store.ReadAsync("OLD", "OLD-1", Now.AddMinutes(-10), CancellationToken.None);

        rows.Select(r => r.RunId).ShouldBe(["fresh"]);
    }

    [Test]
    public async Task Write_WhenAnotherItemOrProjectIsWritten_LeavesTheRowsAlone()
    {
        await _store.WriteAsync("KEEP", "KEEP-1", [Row("a", Now), Row("b", Now)], CancellationToken.None);
        await _store.WriteAsync("KEEP", "KEEP-2", [Row("a", Now)], CancellationToken.None);
        await _store.WriteAsync("KEEP", "KEEP-1", [Row("a", Now.AddMinutes(1))], CancellationToken.None);

        (await CountAsync("KEEP")).ShouldBe(3);
        (await _store.ReadAsync("KEEP", "KEEP-1", Now.AddMinutes(-10), CancellationToken.None)).Count.ShouldBe(2);
    }
}
