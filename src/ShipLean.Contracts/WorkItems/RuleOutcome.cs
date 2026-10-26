namespace ShipLean.Contracts.WorkItems;

public sealed record RuleOutcome(ProposedChange AmendedChange, IReadOnlyList<string> Notices);
