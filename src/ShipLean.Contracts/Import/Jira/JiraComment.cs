namespace ShipLean.Contracts.Import.Jira;

// Body is plain text, whatever Jira's wire format.
public sealed record JiraComment(string Id, JiraUser? Author, string Body, DateTimeOffset Created, DateTimeOffset Updated);
