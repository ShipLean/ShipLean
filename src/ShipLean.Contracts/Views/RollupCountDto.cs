using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Contracts.Views;

// Descendants of one type under a card or row. Closed = Completed category; every other live state is open. InCurrentSprint
// and InOtherSprint are the open ones planned into the current sprint or another sprint (WorkTreeRollupDto says which is which).
public sealed record RollupCountDto(WorkItemTypeId Type, int Open, int Closed, int InCurrentSprint, int InOtherSprint)
{
    public static RollupCountDto From(WorkTreeRollupDto rollup) =>
        new(rollup.Type, rollup.Open, rollup.Closed, rollup.OpenInCurrentSprint, rollup.OpenInOtherSprint);
}
