namespace ShipLean.Contracts.Users.Admin;

// One live membership of a user, for the instance Users table. SoleActiveAdmin: the user is the project's only active
// admin, so disabling or removing them would leave the project without one.
public sealed record AdminUserProjectDto(Guid Id, string Key, string Name, ProjectRole Role, bool SoleActiveAdmin);
