namespace ShipLean.Contracts.Users.Admin;

// A new user (optionally an instance admin), added to the named projects (which the caller must administer) as a Member.
public sealed record AddUserRequest(string Email, string Name, bool IsInstanceAdmin, IReadOnlyList<Guid>? ProjectIds);
