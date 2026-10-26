namespace ShipLean.Contracts.Import.Jira;

// Continuation is null on the last page; pass it back to read the next one.
public sealed record JiraIssuePage(IReadOnlyList<JiraIssue> Issues, string? Continuation);
