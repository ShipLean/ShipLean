namespace ShipLean.Contracts.Import.Jira;

public sealed record JiraAttachment(string Id, string Filename, string MimeType, long Size, string ContentUrl);
