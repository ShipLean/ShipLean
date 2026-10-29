namespace ShipLean.Contracts.Projects;

// The Sprints section. A saved body that leaves SprintNaming out (null) keeps the project's value; a read always carries it.
public sealed record ProjectForecastSettings(int VelocityWindow, decimal? VelocityOverride, SprintNaming? SprintNaming = null);
