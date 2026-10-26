using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Boards.View;

// One lane of a board: stored (from the board's swimlanes) or generated (an Epic's, or No Epic). Criteria is set on a criteria lane.
public sealed record LaneDef(string Id, string Name, Swatch? Colour, BoardLaneKind Kind, string? CriteriaText, ParentRefDto? Epic, bool AcceptsDrops, CriteriaJson? Criteria);
