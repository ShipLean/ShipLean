using ShipLean.Application.Identity;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Features.Users;

// One app_user row as Select returns it; Dapper maps the columns onto the constructor by name. Select needs @now (see
// UserStatusRule).
internal sealed record UserRow(
    Guid Id, string Name, string Email, string Initials, string AvatarColour, string? PhotoUrl, bool IsInstanceAdmin, string Status)
{
    public const string Select =
        $"""
        SELECT u.id, u.name, u.email, u.initials, u.avatar_colour AS avatarColour,
               {UserRefs.PhotoUrlSql} AS photoUrl,
               u.is_admin AS isInstanceAdmin, {UserStatusRule.Sql} AS status
        FROM app_user u
        """;

    public UserDto ToDto() => new(Id, Name, Email, Initials, AvatarColour, PhotoUrl, IsInstanceAdmin, UserStatusRule.Parse(Status));
}
