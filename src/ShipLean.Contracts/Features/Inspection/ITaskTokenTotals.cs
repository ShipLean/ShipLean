namespace ShipLean.Contracts.Inspection;

// Tokens per task from the agent runs in the telemetry store (SHIP-1398 CK1), for Inspection's Cost view. A run counts only toward
// the items at its strongest link with seen_at in [From, To] (Tagged > ShipleanFile > FromToolCall); when several items share that
// link its tokens are split evenly over them (integer division, the remainder to the first key in ordinal order) and each gets one
// run (item D21). A run's tokens are its spans' when any span reported them, else its logs'; a run that
// reported no tokens at all is not a counted run (no Runs, no ByItem entry from it). ReadAsync never throws for an
// unreachable or unsupported store: it returns Available false with a Problem and no items.
public interface ITaskTokenTotals
{
    Task<TaskTokenTotals> ReadAsync(TaskTokenQuery query, CancellationToken ct);
}
