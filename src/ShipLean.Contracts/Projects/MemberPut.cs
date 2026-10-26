namespace ShipLean.Contracts.Projects;

// One row of the Members section as saved: the member is named by email.
public sealed record MemberPut(string Email, ProjectRole Role);
