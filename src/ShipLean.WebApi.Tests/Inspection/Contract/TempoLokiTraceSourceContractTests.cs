using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.TempoLoki;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

namespace ShipLean.WebApi.Tests.Inspection.Contract;

// Lane B (SHIP-1558): red against the stubs until B1 (find runs) and B2 (get run) land.
[TestFixture(TestName = "Tempo and Loki store, SHIP-1558")]
[Category("RedUntilLane")] // Lane B removes this category when the suite goes green, so CI stops excluding it.
[Category("SHIP-1558")]
public sealed class TempoLokiTraceSourceContractTests : TraceSourceContractTests
{
    protected override string Lane => "SHIP-1558";

    protected override string Store => "tempo-loki";

    protected override ITraceSource CreateSource(InspectionOptions options) => Build(options);

    // The one place the source's parts are built; lane B owns it and gives the parts their settings and HTTP clients here.
    private static TempoLokiTraceSource Build(InspectionOptions options)
    {
        var clients = StoreClients.Create(options);
        var cache = new RunLinkCache(new InMemoryRunLinkStore(), TimeProvider.System);
        return new TempoLokiTraceSource(
            new TempoLokiRunFinder(new TempoRunFinder(clients, options), new LokiRunFinder(clients, options), cache, options, TimeProvider.System),
            new TempoLokiRunReader(),
            new TempoLokiHealth(clients, options, TimeProvider.System),
            new TempoLokiFieldCheck(clients, options, TimeProvider.System));
    }
}
