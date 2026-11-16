namespace ShipLean.Contracts.Demos;

// The sprint's three numbers on the Demo tab: Committed and Completed as ISprintMetrics defines them, FeaturesMoved the Features
// that hold the sprint's stories and bugs (ISprintFeatureReads, the Features the demo makes a chapter for).
public sealed record SprintDemoNumbersDto(decimal Committed, decimal Completed, int FeaturesMoved);
