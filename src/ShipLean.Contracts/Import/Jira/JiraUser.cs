namespace ShipLean.Contracts.Import.Jira;

// Email is null when the user hides it. On Server the AccountId is the user key. IsBot: Jira Cloud's accountType "app" (Automation for Jira, integrations); Server has no such type, so it is false there.
public sealed record JiraUser(string AccountId, string DisplayName, string? Email, bool IsBot);
