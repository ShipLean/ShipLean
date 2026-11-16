namespace ShipLean.Contracts.Users;

// The signed-in user: every UserDto field, then what the shell needs to draw itself. TimeZone is the user's IANA id (null
// when not set, and every label then reads in UTC); TimeZoneLabel names it with its offset, or says it is not set.
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
    Density Density,
    string? TimeZone,
    string TimeZoneLabel);
