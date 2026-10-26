using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Import;

// Resolution Matched and Existing carry User; Create makes a user without a password from Name and Email; JiraImportUser
// attributes the work to the seeded Jira import user. IsBot: an automation account (Jira Cloud accountType "app"); it is never offered Create and defaults to JiraImportUser.
public sealed record UserMappingDto(string JiraAccountId, string Name, string? Email, int References, UserResolution Resolution, UserRef? User, bool IsBot, bool IsNewSinceLastRun);
