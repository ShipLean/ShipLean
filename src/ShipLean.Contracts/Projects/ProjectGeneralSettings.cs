namespace ShipLean.Contracts.Projects;

// AgentConnections are the connections the admin can pick (plus the saved choice, flagged, if it was archived); RepoBaseBranch
// is as saved, null meaning main; HolidayCountry is the ISO code of the country whose public holidays the project skips, null meaning None. BugSeverityWeights maps
// a bug's severity (1 critical to 4 low) to its weight in Inspection's weighted escaping-bug count (SHIP-1398 item D16; default 8/4/2/1).
public sealed record ProjectGeneralSettings(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<int> WorkingDays,
    Guid? AgentConnectionId,
    string? RepoUrl,
    string? RepoBaseBranch,
    string? HolidayCountry,
    IReadOnlyList<AgentConnectionOption> AgentConnections,
    IReadOnlyDictionary<int, int> BugSeverityWeights);
