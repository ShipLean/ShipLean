using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Identity;

// How an app_user row becomes a UserRef. Every read model that joins users selects Columns and lets Dapper map the
// result straight onto UserRef (column names match its constructor). The alias is fixed to `u`; a read model with
// several users (assignee, creator) joins each through a lateral subquery:
//   LEFT JOIN LATERAL (SELECT {UserRefs.Columns} FROM app_user u WHERE u.id = w.assigned_to) a ON true
public static class UserRefs
{
    // The columns of a UserRef for an app_user aliased `u`: SELECT {UserRefs.Columns} FROM app_user u ...
    public const string Columns = $"u.id, u.name, u.initials, u.avatar_colour AS avatarColour, {PhotoUrlSql} AS photoUrl";

    // The photo URL of app_user `u` (with ?v=photo_version), or null without a photo; PhotoUrl is the same rule in C#.
    public const string PhotoUrlSql =
        "CASE WHEN u.avatar_photo_blob IS NOT NULL THEN '/api/users/' || u.id::text || '/photo?v=' || u.photo_version END";

    // ?v= is photo_version, a cache buster bumped on every upload, replace or remove (no history is kept).
    public static string? PhotoUrl(Guid id, bool hasPhoto, int photoVersion) => hasPhoto ? $"/api/users/{id}/photo?v={photoVersion}" : null;

    public static UserRef From(UserDto user) => new(user.Id, user.Name, user.Initials, user.AvatarColour, user.PhotoUrl);
}
