namespace ShipLean.Application.Features.States;

// What a set of checked changes will write: the states that stay (new ones flagged), the removals with their targets.
internal sealed record StatePlan(List<StateDraft> Remaining, List<(StateDraft State, StateDraft Target)> Removals);
