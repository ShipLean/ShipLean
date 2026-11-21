namespace ShipLean.Contracts.IssueIntake;

// The project that takes a repository's issues, as the intake lanes need it. Repository is the stored "owner/name".
// RunAsUserId creates the proposal sessions (D8) and is null until an admin chooses one. The proposal instructions
// are the project's Issue proposal prompt (D7), resolved by IPromptResolver. MaxRounds caps the rounds (D11).
public sealed record IssueIntakeProject(Guid ProjectId, string ProjectKey, string Repository, Guid? RunAsUserId, int MaxRounds);
