namespace ShipLean.Contracts.Import.Jira;

// IssueTypes: the names of the issue types that use this status in the project.
public sealed record JiraStatus(string Id, string Name, JiraStatusCategory Category, IReadOnlyList<string> IssueTypes);
