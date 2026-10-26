using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.ProjectSettings;

// A live member as Select returns it (needs @projectId and @now); Dapper maps the columns onto the constructor by name.
internal sealed record MemberRow(Guid Id, string Name, string Initials, string AvatarColour, string? PhotoUrl, string Email, string Role, string Status)
{
    public const string Select =
        $"""
        SELECT {UserRefs.Columns}, u.email, m.role, {UserStatusRule.Sql} AS status
        FROM project_member m JOIN app_user u ON u.id = m.user_id
        WHERE m.project_id = @projectId AND m.archived_at IS NULL
        ORDER BY u.name, u.email
        """;

    public MemberDto ToDto() => new(new UserRef(Id, Name, Initials, AvatarColour, PhotoUrl), Email, Enum.Parse<ProjectRole>(Role, ignoreCase: true), UserStatusRule.Parse(Status));
}
