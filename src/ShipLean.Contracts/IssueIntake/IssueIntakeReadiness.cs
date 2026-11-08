namespace ShipLean.Contracts.IssueIntake;

// What intake needs that lives outside its own settings, each true when it is in place, so the settings page can say
// what is missing: the project's repo_url names a github.com repository, the project has an agent connection, the
// instance has a GitHub token and a webhook secret, and a run-as member is chosen.
public sealed record IssueIntakeReadiness(bool HasGitHubRepository, bool HasAgentConnection, bool HasGitHubToken, bool HasWebhookSecret, bool HasRunAs);
