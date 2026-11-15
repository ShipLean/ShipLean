using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.ClickHouse;
using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Contracts.Inspection;
using ShipLean.LocalEnvironment.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.ClickHouse;

// A throwaway exporter database next to the stack's own, with the exporter's tables cut down to the columns Inspection reads, and
// a throwaway `inspection` database beside it, so a schema test migrates its own tables without touching the shared ones.
public sealed class ScratchExporter : IAsyncDisposable
{
    public const string Project = "SCR";
    private static readonly DateTime Origin = new(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc);

    private ScratchExporter(string exporter, string inspection, InspectionOptions options)
        => (ExporterDatabase, InspectionDatabase, Options, Db) = (exporter, inspection, options, ClickHouseDb.From(options));

    public string ExporterDatabase { get; }

    public string InspectionDatabase { get; }

    public InspectionOptions Options { get; }

    public ClickHouseDb Db { get; }

    public static async Task<ScratchExporter> CreateAsync(string? tracesTableColumns = null)
    {
        var stack = await InspectionStack.GetAsync();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var (exporter, inspection) = ("scratch_otel_" + suffix, "scratch_inspection_" + suffix);
        var options = stack.Options("clickhouse") with { ClickHouseDatabase = exporter };
        var scratch = new ScratchExporter(exporter, inspection, options);
        await scratch.Db.ExecuteAsync($"CREATE DATABASE {exporter}", null, CancellationToken.None);
        await scratch.Db.ExecuteAsync(
            $"""
            CREATE TABLE {exporter}.otel_traces ({tracesTableColumns ?? TraceColumns})
            ENGINE = MergeTree ORDER BY (ServiceName, Timestamp)
            """,
            null,
            CancellationToken.None);
        await scratch.Db.ExecuteAsync(
            $"""
            CREATE TABLE {exporter}.otel_logs (
                Timestamp DateTime64(9), ServiceName LowCardinality(String), SeverityText LowCardinality(String), SeverityNumber UInt8,
                ResourceAttributes Map(LowCardinality(String), String), LogAttributes Map(LowCardinality(String), String),
                Body String, TraceId String, SpanId String)
            ENGINE = MergeTree ORDER BY (ServiceName, Timestamp)
            """,
            null,
            CancellationToken.None);
        return scratch;
    }

    public ClickHouseSchemaMigrator Migrator() => new(Options, InspectionDatabase);

    public ClickHouseSchemaMigrator Migrator(InspectionOptions options) => new(options, InspectionDatabase);

    public ClickHouseRunFinder Finder(ClickHouseSchemaMigrator migrator) => new(Options, migrator);

    // One span, `seconds` after the origin and lasting `lasting` seconds, with the project and session on its resource.
    public Task AddSpanAsync(string session, int seconds, int lasting, string status, IReadOnlyDictionary<string, string> spanAttributes, IReadOnlyDictionary<string, string>? extraResource = null)
        => Db.ExecuteAsync(
            $$"""
            INSERT INTO {{ExporterDatabase}}.otel_traces (Timestamp, SpanName, ServiceName, ResourceAttributes, SpanAttributes, Duration, StatusCode)
            VALUES ({at:DateTime64(9, 'UTC')}, {name:String}, 'claude-code', {resource:Map(String, String)}, {attributes:Map(String, String)}, {duration:UInt64}, {status:String})
            """,
            new Dictionary<string, object>
            {
                ["at"] = Origin.AddSeconds(seconds),
                ["name"] = spanAttributes.GetValueOrDefault("span.name", "claude_code.tool"),
                ["resource"] = new Dictionary<string, string>(extraResource ?? new Dictionary<string, string>()) { ["shiplean.project"] = Project, ["user.email"] = "dev@example.com" },
                ["attributes"] = new Dictionary<string, string>(spanAttributes) { ["session.id"] = session },
                ["duration"] = (ulong)lasting * 1_000_000_000UL,
                ["status"] = status,
            },
            CancellationToken.None);

    public Task AddLogAsync(string session, int seconds, string severity, IReadOnlyDictionary<string, string> logAttributes)
        => Db.ExecuteAsync(
            $$"""
            INSERT INTO {{ExporterDatabase}}.otel_logs (Timestamp, ServiceName, SeverityText, SeverityNumber, ResourceAttributes, LogAttributes)
            VALUES ({at:DateTime64(9, 'UTC')}, 'claude-code', {severity:String}, 9, {resource:Map(String, String)}, {attributes:Map(String, String)})
            """,
            new Dictionary<string, object>
            {
                ["at"] = Origin.AddSeconds(seconds),
                ["severity"] = severity,
                ["resource"] = new Dictionary<string, string> { ["shiplean.project"] = Project },
                ["attributes"] = new Dictionary<string, string>(logAttributes) { ["session.id"] = session },
            },
            CancellationToken.None);

    public static RunQuery Query(string item) => new(Project, item, Origin.AddMinutes(-1), Origin.AddHours(1), null, null);

    public async ValueTask DisposeAsync()
    {
        await Db.ExecuteAsync($"DROP DATABASE IF EXISTS {ExporterDatabase}", null, CancellationToken.None);
        await Db.ExecuteAsync($"DROP DATABASE IF EXISTS {InspectionDatabase}", null, CancellationToken.None);
    }

    private const string TraceColumns = """
        Timestamp DateTime64(9), TraceId String, SpanId String, ParentSpanId String, SpanName LowCardinality(String), SpanKind LowCardinality(String),
        ServiceName LowCardinality(String), ResourceAttributes Map(LowCardinality(String), String), SpanAttributes Map(LowCardinality(String), String),
        Duration UInt64, StatusCode LowCardinality(String), StatusMessage String,
        `Events.Timestamp` Array(DateTime64(9)), `Events.Name` Array(LowCardinality(String)), `Events.Attributes` Array(Map(LowCardinality(String), String))
        """;
}
