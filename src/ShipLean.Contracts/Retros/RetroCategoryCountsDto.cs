namespace ShipLean.Contracts.Retros;

public sealed record RetroCategoryCountsDto(int WentWell, int NotInPlan, int WentBadly, int Improve);
