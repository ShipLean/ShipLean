namespace ShipLean.Contracts.Users;

// A user as the instance's user pages and the by-email lookup's callers see them. Carries every UserRef field.
public sealed record UserDto(
    Guid Id,
    string Name,
    string Email,
    string Initials,
    string AvatarColour,
    string? PhotoUrl,
    bool IsInstanceAdmin,
    UserStatus Status);
