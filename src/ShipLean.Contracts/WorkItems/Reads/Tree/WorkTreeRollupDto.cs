using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems.Reads.Tree;

// The descendants of one type under one parent, at any depth (a nested Feature's children count too, and so does the nested
// Feature). Closed = Completed category, open = every other live state; Removed items are not counted.
//
// Points are the story points of the counted items. OpenPoints covers the open ones, TotalPoints open and closed together.
// Both are null when none of the counted items has points (nothing estimated is not "0 points"); items without points add
// nothing to a sum, and Unestimated counts them (open and closed) so a forecast can say "plus N unestimated".
//
// Every descendant type is rolled up, Tasks included. The Features and Epics boards draw only the Feature, Story and Bug
// entries and drop the Task entries. A type with no counted descendants has no entry at all: a missing entry means zero.
// Tasks under a closed story still count as open here, so a board's offerDone check (offer Done once everything is closed)
// must ignore them rather than read them from this roll-up.
public sealed record WorkTreeRollupDto(Guid ParentId, WorkItemTypeId Type, int Open, int Closed, decimal? OpenPoints, decimal? TotalPoints, int Unestimated);
