using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

public sealed class TempoLokiFieldCheck
{
    // TODO: check each configured field against recent data (lane B field check).
    // Instance method so the lane can add dependencies to this part; S2325 clears when it does (it uses no state in the stub).
#pragma warning disable S2325
    public Task<IReadOnlyList<FieldCheck>> CheckAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<FieldCheck>>([]);
#pragma warning restore S2325
}
