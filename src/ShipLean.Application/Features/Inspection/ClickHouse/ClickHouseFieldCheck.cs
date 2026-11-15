using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

internal static class ClickHouseFieldCheck
{
    // TODO: check each configured field against recent data (lane A field check).
    public static Task<IReadOnlyList<FieldCheck>> CheckAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<FieldCheck>>([]);
}
