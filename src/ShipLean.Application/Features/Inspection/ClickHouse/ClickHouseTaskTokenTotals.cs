using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

// Tokens per task from inspection.runs joined to inspection.run_items (SHIP-1398 CK1), for the ClickHouse store.
public sealed class ClickHouseTaskTokenTotals : ITaskTokenTotals
{
    public const string NotBuilt = "Tokens per task are not read from ClickHouse yet.";

    // TODO: a stub until the ClickHouse token query lands; it reads nothing and reports the store unavailable.
    public Task<TaskTokenTotals> ReadAsync(TaskTokenQuery query, CancellationToken ct) => Task.FromResult(TaskTokenTotals.Unavailable(NotBuilt));
}
