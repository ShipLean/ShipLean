namespace ShipLean.Contracts.Users.Admin;

// A user as the instance admin's Users table sees them. LastSignIn is a server label (null: never signed in).
public sealed record AdminUserDto(
    Guid Id,
    string Name,
    string Email,
    string Initials,
    string AvatarColour,
    string? PhotoUrl,
    bool IsInstanceAdmin,
    UserStatus Status,
    string? LastSignIn,
    IReadOnlyList<AdminUserProjectDto> Projects);
