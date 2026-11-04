namespace ShipLean.Contracts.Demos;

public sealed record SprintDemoRetroData(
    IReadOnlyList<SprintDemoRetroPoint> WentWell,
    IReadOnlyList<SprintDemoRetroPoint> WentBadly,
    IReadOnlyList<SprintDemoRetroPoint> Improve,
    IReadOnlyList<SprintDemoRetroPoint> NotInPlan);
