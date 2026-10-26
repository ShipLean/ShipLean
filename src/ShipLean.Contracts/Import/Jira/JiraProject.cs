namespace ShipLean.Contracts.Import.Jira;

public sealed record JiraProject(string Key, string Name, string TypeKey, int IssueCount);
