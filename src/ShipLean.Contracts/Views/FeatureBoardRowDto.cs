using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;

namespace ShipLean.Contracts.Views;

// A TaskboardRowDto with a parent always (no Unparented row) plus the section it sits in (FeatureId) and the story's sprint
// (null = the Backlog). Cells has one entry per column, in column order.
public sealed record FeatureBoardRowDto(CardDto Parent, bool OfferResolve, IReadOnlyList<TaskboardCellDto> Cells, Guid FeatureId, SprintRef? Sprint, NeedsYouDto? NeedsYou);
