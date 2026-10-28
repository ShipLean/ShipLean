namespace ShipLean.Contracts.Projects;

// The General section as saved: the key is chosen at creation and never changes. The agent fields are the whole value:
// null (or blank text) clears them.
public sealed record ProjectGeneralPut(
    string Name, string Description, IReadOnlyList<int> WorkingDays, Guid? AgentConnectionId = null, string? RepoUrl = null, string? RepoBaseBranch = null);
