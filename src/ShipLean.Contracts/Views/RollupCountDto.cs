using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views;

// Descendants of one type under a card or row. Closed = Completed category; every other live state is open.
public sealed record RollupCountDto(WorkItemTypeId Type, int Open, int Closed);
