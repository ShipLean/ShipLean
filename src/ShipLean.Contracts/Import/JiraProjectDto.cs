namespace ShipLean.Contracts.Import;

// ImportedAs: the latest finished import of this Jira project, null when it was never imported.
public sealed record JiraProjectDto(string Key, string Name, int IssueCount, ImportedAsDto? ImportedAs);
