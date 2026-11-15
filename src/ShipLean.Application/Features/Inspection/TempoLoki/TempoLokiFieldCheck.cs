using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

internal static class TempoLokiFieldCheck
{
    // TODO: check each configured field against recent data (lane B field check).
    public static Task<IReadOnlyList<FieldCheck>> CheckAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<FieldCheck>>([]);
}
