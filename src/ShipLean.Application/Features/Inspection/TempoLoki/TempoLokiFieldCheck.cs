using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

public sealed class TempoLokiFieldCheck
{
    // TODO: check each configured field against recent data (lane B field check).
    // Instance method so the lane can add dependencies to this part; the stub returns a field so S2325 stays quiet without a suppression.
    private readonly Task<IReadOnlyList<FieldCheck>> notBuilt = Task.FromResult<IReadOnlyList<FieldCheck>>([]);
    public Task<IReadOnlyList<FieldCheck>> CheckAsync(CancellationToken ct) => notBuilt;
}
