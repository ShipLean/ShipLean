using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.TempoLoki;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.Contract;

// Lane B (SHIP-1558): red against the stubs until B1 (find runs) and B2 (get run) land.
[TestFixture(TestName = "Tempo and Loki store, SHIP-1558")]
[Category("SHIP-1558")]
public sealed class TempoLokiTraceSourceContractTests : TraceSourceContractTests
{
    protected override string Lane => "SHIP-1558";

    protected override string Store => "tempo-loki";

    protected override ITraceSource CreateSource(InspectionOptions options) => Build();

    // The one place the source's parts are built; lane B owns it and gives the parts their settings and HTTP clients here.
    private static TempoLokiTraceSource Build()
        => new(new TempoLokiRunFinder(), new TempoLokiRunReader(), new TempoLokiHealth(), new TempoLokiFieldCheck());
}
