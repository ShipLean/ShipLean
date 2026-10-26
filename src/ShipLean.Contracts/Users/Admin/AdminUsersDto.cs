namespace ShipLean.Contracts.Users.Admin;

// The whole Users tab in one response.
public sealed record AdminUsersDto(IReadOnlyList<AdminUserDto> Users, IReadOnlyList<AdminProjectChoiceDto> AdministeredProjects);
