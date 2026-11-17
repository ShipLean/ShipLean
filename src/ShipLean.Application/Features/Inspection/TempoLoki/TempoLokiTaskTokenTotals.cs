using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

// Tempo and Loki have no index to total tokens over hundreds of tasks (runs are found one item at a time), so Inspection's Cost view
// needs the ClickHouse store (SHIP-1398 item D20).
public sealed class TempoLokiTaskTokenTotals : ITaskTokenTotals
{
    public const string NeedsClickHouse = "Tokens per task needs the ClickHouse store.";

    public Task<TaskTokenTotals> ReadAsync(TaskTokenQuery query, CancellationToken ct) => Task.FromResult(TaskTokenTotals.Unavailable(NeedsClickHouse));
}
