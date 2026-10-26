using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Auth;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Identity;

// GET /api/me: the signed-in user as the shell draws itself, read by SQL in one statement.
[RegisterSingleton]
public sealed class MeReader(NpgsqlDataSource dataSource, TimeProvider clock, ProxySignInSettings proxy)
{
    private sealed record Row(
        Guid Id, string Name, string Email, string Initials, string AvatarColour, string? PhotoUrl, bool IsInstanceAdmin,
        string Status, bool HasProjects, bool CanCreateProjects, string? Theme, string? Density);

    private const string Sql =
        $$"""
        SELECT u.id, u.name, u.email, u.initials, u.avatar_colour AS avatarColour,
               {{UserRefs.PhotoUrlSql}} AS photoUrl,
               u.is_admin AS isInstanceAdmin,
               ({{UserStatusRule.Sql}}) AS status,
               EXISTS (SELECT 1 FROM project_member m WHERE m.user_id = u.id AND m.archived_at IS NULL) AS hasProjects,
               (u.is_admin OR (SELECT s.project_creation FROM instance_settings s) = 'everyone') AS canCreateProjects,
               (SELECT p.json #>> '{}' FROM user_preference p WHERE p.user_id = u.id AND p.key = 'theme') AS theme,
               (SELECT p.json #>> '{}' FROM user_preference p WHERE p.user_id = u.id AND p.key = 'density') AS density
        FROM app_user u
        WHERE u.id = @id
        """;

    public async Task<MeDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            Sql, new { id = userId, now = clock.GetUtcNow() }, cancellationToken: cancellationToken))
            ?? throw ApiException.Unauthorized("Sign in to continue.");
        return new MeDto(
            row.Id, row.Name, row.Email, row.Initials, row.AvatarColour, row.PhotoUrl, row.IsInstanceAdmin,
            UserStatusRule.Parse(row.Status), row.HasProjects, row.CanCreateProjects,
            Enum.TryParse<Theme>(row.Theme, ignoreCase: true, out var theme) ? theme : Theme.Os,
            Enum.TryParse<Density>(row.Density, ignoreCase: true, out var density) ? density : Density.Compact);
    }

    public async Task<InstanceDto> InstanceAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var name = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT name FROM instance_settings", cancellationToken: cancellationToken)) ?? "ShipLean";
        return new InstanceDto(name, proxy.IsProxy ? SignInMode.Proxy : SignInMode.Password, proxy.SignOutUrl);
    }
}
