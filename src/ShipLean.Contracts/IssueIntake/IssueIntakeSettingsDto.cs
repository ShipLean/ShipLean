using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.IssueIntake;

// GET and PUT /api/projects/{id}/settings/issue-intake. Repository is "owner/name": the stored one while intake is on,
// otherwise the one the project's repo_url names, and null when repo_url names no github.com repository. RunAs is the
// member whose name proposal sessions run under, null until chosen. The proposal instructions live in Prompts
// (the Issue proposal prompt). MaxRounds is the round cap (D11, 5 by default).
public sealed record IssueIntakeSettingsDto(
    bool Enabled,
    string? Repository,
    UserRef? RunAs,
    int MaxRounds,
    IssueIntakeReadiness Readiness);
