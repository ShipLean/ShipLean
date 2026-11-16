using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.TempoLoki;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

namespace ShipLean.WebApi.Tests.Inspection.Contract;

// Lane B (SHIP-1558): the shared contract suite over the Tempo and Loki store.
[TestFixture(TestName = "Tempo and Loki store, SHIP-1558")]
[Category("SHIP-1558")]
public sealed class TempoLokiTraceSourceContractTests : TraceSourceContractTests
{
    protected override string Lane => "SHIP-1558";

    protected override string Store => "tempo-loki";

    // Codex CLI and Cursor have no recording yet (D27).
    protected override IReadOnlyCollection<string> RecordedNames => ["claude-code-recorded"];

    protected override ITraceSource CreateSource(InspectionOptions options) => Build(options);

    // The one place the source's parts are built; lane B owns it and gives the parts their settings and HTTP clients here.
    private static TempoLokiTraceSource Build(InspectionOptions options)
    {
        var clients = StoreClients.Create(options);
        var cache = new RunLinkCache(new InMemoryRunLinkStore(), TimeProvider.System);
        return new TempoLokiTraceSource(
            new TempoLokiRunFinder(new TempoRunFinder(clients, options), new LokiRunFinder(clients, options), cache, options, TimeProvider.System),
            new TempoLokiRunReader(new TempoTraceLoader(clients, options), new LokiLogReader(new LokiRunFinder(clients, options), options), options, TimeProvider.System),
            new TempoLokiHealth(clients, options, TimeProvider.System),
            new TempoLokiFieldCheck(clients, options, TimeProvider.System));
    }
}
