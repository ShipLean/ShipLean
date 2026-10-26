namespace ShipLean.Contracts.Import.Jira;

public sealed record JiraIssueLink(string TypeName, JiraLinkDirection Direction, string OtherKey);
