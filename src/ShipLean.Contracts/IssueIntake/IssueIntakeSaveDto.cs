namespace ShipLean.Contracts.IssueIntake;

// The PUT body. Turning intake on stores the repository the project's repo_url names (D5). Empty ProposalInstructions
// means the built-in default. Refusals are ApiError codes repository_taken (another project takes that repository),
// no_repo_url (repo_url names no github.com repository) and not_member (RunAsUserId is not a member), with the field at fault.
public sealed record IssueIntakeSaveDto(bool Enabled, Guid? RunAsUserId, string ProposalInstructions, int MaxRounds);
