namespace ShipLean.Contracts.Users;

// The signed-in user: every UserDto field, then what the shell needs to draw itself.
public sealed record MeDto(
    Guid Id,
    string Name,
    string Email,
    string Initials,
    string AvatarColour,
    string? PhotoUrl,
    bool IsInstanceAdmin,
    UserStatus Status,
    bool HasProjects,
    bool CanCreateProjects,
    Theme Theme,
    Density Density);
