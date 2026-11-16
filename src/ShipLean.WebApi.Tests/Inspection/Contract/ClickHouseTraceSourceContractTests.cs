using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.ClickHouse;
using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.Contract;

// Lane A (SHIP-1555): red against the stubs until A1 (find runs) and A2 (get run) land.
[TestFixture(TestName = "ClickHouse store, SHIP-1555")]
[Category("SHIP-1555")]
public sealed class ClickHouseTraceSourceContractTests : TraceSourceContractTests
{
    protected override string Lane => "SHIP-1555";

    protected override string Store => "clickhouse";

    // Codex CLI and Cursor have no recording yet (D27).
    protected override IReadOnlyCollection<string> RecordedNames => ["claude-code-recorded"];

    protected override ITraceSource CreateSource(InspectionOptions options) => Build(options);

    // The one place the source's parts are built; lane A owns it and gives the parts their settings and connections here.
    private static ClickHouseTraceSource Build(InspectionOptions options)
    {
        var migrator = new ClickHouseSchemaMigrator(options);
        var finder = new ClickHouseRunFinder(options, migrator);
        return new(finder, new ClickHouseRunReader(options, migrator, finder), new ClickHouseHealth(options, migrator), new ClickHouseFieldCheck(options, migrator));
    }
}
