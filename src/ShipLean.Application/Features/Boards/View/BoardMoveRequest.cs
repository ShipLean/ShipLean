using System.Text.Json.Serialization;

namespace ShipLean.Application.Features.Boards.View;

// A card dropped on a level board. LaneId is the lane dropped into (a stored lane's id, "epic-<epic id>" or "lane-no-epic");
// null keeps the card's lane. BeforeItemId places the card before another card of the column (null = last).
public sealed record BoardMoveRequest(
    [property: JsonRequired] Guid ItemId,
    [property: JsonRequired] int ExpectedRevision,
    [property: JsonRequired] string ColumnId,
    bool Done,
    string? LaneId,
    Guid? BeforeItemId);
