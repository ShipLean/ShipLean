namespace ShipLean.Contracts.Demos;

// One period a task of the Feature spent Blocked.
public sealed record SprintDemoBlocked(string TaskKey, string Title, double Hours, string Label);
