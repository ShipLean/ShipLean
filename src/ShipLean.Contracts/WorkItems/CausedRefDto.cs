namespace ShipLean.Contracts.WorkItems;

// A Bug that points at this item with a Caused by link (SHIP-1398 item D18), shown as a Caused row on the Task. LinkDto is outgoing only.
public sealed record CausedRefDto(Guid Id, string Key, string Title);
