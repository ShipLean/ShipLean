using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Import;

// The Jira site a project's admins imported from. The token is never returned: only its last four characters.
// Kind decides the sign-in: Cloud = email + API token, Server = personal access token (Email may be empty).
public sealed record JiraConnectionDto(Guid Id, Guid ProjectId, string BaseUrl, JiraKind Kind, string Email, string TokenLastFour, UserRef? CreatedBy, DateTimeOffset CreatedAt, string CreatedLabel);
