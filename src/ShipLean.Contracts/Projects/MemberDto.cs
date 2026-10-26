using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Users;

namespace ShipLean.Contracts.Projects;

public sealed record MemberDto(UserRef User, string Email, ProjectRole Role, UserStatus Status);
