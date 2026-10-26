namespace ShipLean.Contracts.Import.Jira;

public sealed record JiraField(string Id, string Name, bool Custom, string? SchemaType);
