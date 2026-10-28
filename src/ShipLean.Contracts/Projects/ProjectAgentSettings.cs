namespace ShipLean.Contracts.Projects;

// A project's agent settings as the dispatch code reads them. BaseBranch is resolved ("main" when none is saved).
public sealed record ProjectAgentSettings(Guid? ConnectionId, string? RepoUrl, string BaseBranch);
