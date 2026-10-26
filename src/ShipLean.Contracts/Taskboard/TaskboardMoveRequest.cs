using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Taskboard;

// A Taskboard drop. StateId is the column (a Task state). Row is null when the drop stays in its row. BeforeItemId is the
// task the card was dropped above in the target cell; null places it last in the cell. A People drop (Row.Grouping People)
// ignores BeforeItemId: a People cell mixes parents while the store's cell is (state, parent), so the task goes last in its
// (state, parent) cell. People cells list tasks in parent row order, then cell order.
public sealed record TaskboardMoveRequest(
    [property: JsonRequired] Guid TaskId,
    [property: JsonRequired] int ExpectedRevision,
    [property: JsonRequired] Guid StateId,
    TaskboardMoveRow? Row,
    Guid? BeforeItemId);
