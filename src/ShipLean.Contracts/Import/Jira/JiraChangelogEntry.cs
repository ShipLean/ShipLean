namespace ShipLean.Contracts.Import.Jira;

public sealed record JiraChangelogEntry(string Id, JiraUser? Author, DateTimeOffset Created, IReadOnlyList<JiraChangeItem> Items);
