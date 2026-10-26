namespace ShipLean.Contracts.Import.Jira;

// From and To are Jira's display strings for the old and new value.
public sealed record JiraChangeItem(string Field, string? From, string? To);
