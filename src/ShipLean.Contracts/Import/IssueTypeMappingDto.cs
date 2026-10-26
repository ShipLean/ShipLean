namespace ShipLean.Contracts.Import;

// Note: the reason shown beside a default ("Epics become Features"). Count: issues of that type in the Jira project.
public sealed record IssueTypeMappingDto(string JiraType, int Count, ImportTypeTarget Target, string? Note, bool IsNewSinceLastRun);
