using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Contracts.Views;

// Epic is set on a generated Epic lane (the header draws its key). AcceptsDrops is false for a lane whose criteria are not all
// "=" clauses: a card cannot be dropped there, because no single field change would put it there. CriteriaText is the lane's criteria in words ("Priority = 1"), null for lanes without.
public sealed record BoardLaneDto(
    string Id,
    string Name,
    Swatch? Colour,
    BoardLaneKind Kind,
    string? CriteriaText,
    ParentRefDto? Epic,
    bool AcceptsDrops,
    int CardCount,
    decimal OpenPoints,
    IReadOnlyList<BoardCellDto> Cells);
