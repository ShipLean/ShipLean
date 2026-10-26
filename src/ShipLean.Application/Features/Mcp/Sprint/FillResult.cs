using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Mcp.Sprint;

// Selected: the items under the cut line. Left: the stack below it. SprintPoints: what the sprint holds after the
// selection. OverBy: how far past the room that is (null when it fits). Capped: the selection stopped at MaxItems.
public sealed record FillResult(IReadOnlyList<StackRowDto> Selected, IReadOnlyList<StackRowDto> Left, decimal SprintPoints, decimal? OverBy, bool Capped);
