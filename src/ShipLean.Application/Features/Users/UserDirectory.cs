using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Core.People;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Features.Users;

public sealed class UserDirectory(NpgsqlDataSource dataSource, TimeProvider clock) : IUserDirectory
{
    public async Task<UserDto?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
            $"{UserRow.Select} WHERE lower(u.email) = lower(@email)", new { email = (email ?? string.Empty).Trim(), now = clock.GetUtcNow() },
            cancellationToken: cancellationToken));
        return row?.ToDto();
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
            $"{UserRow.Select} WHERE u.id = @id", new { id, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
        return row?.ToDto() ?? throw AccountRules.UserNotFound(id);
    }

    public async Task<UserDto> CreateAsync(string email, string name, CancellationToken cancellationToken = default)
    {
        var cleanEmail = UserInput.RequireEmail(email);
        var cleanName = UserInput.RequireName(name);
        var id = Guid.CreateVersion7();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var inserted = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            INSERT INTO app_user (id, email, name, initials, avatar_colour, security_stamp, created_at, updated_at)
            VALUES (@id, @email, @name, @initials, @colour, @stamp, @now, @now)
            ON CONFLICT (lower(email)) DO NOTHING
            RETURNING id
            """,
            new
            {
                id,
                email = cleanEmail,
                name = cleanName,
                initials = Initials.From(cleanName),
                colour = AvatarPalette.For(cleanEmail),
                stamp = AccountRules.NewStamp(),
                now = clock.GetUtcNow(),
            },
            cancellationToken: cancellationToken));
        if (inserted is null)
        {
            throw ApiException.Conflict($"A user with the email {cleanEmail} already exists.");
        }
        return await GetAsync(id, cancellationToken);
    }

    public async Task<UserDto> FindOrCreateAsync(string email, string? name, CancellationToken cancellationToken = default)
    {
        var cleanEmail = UserInput.RequireEmail(email);
        var usable = UserInput.UsableName(name);
        var cleanName = usable ?? UserInput.NameFromEmail(cleanEmail);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // RETURNING gives the id for a create or a rename; an existing user left alone returns no row.
        var id = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            INSERT INTO app_user (id, email, name, initials, avatar_colour, security_stamp, created_at, updated_at)
            VALUES (@id, @email, @name, @initials, @colour, @stamp, @now, @now)
            ON CONFLICT (lower(email)) DO UPDATE
                SET name = EXCLUDED.name, initials = EXCLUDED.initials, updated_at = @now
                WHERE @hasName AND app_user.name IS DISTINCT FROM EXCLUDED.name
            RETURNING id
            """,
            new
            {
                id = Guid.CreateVersion7(),
                email = cleanEmail,
                name = cleanName,
                initials = Initials.From(cleanName),
                colour = AvatarPalette.For(cleanEmail),
                stamp = AccountRules.NewStamp(),
                now = clock.GetUtcNow(),
                hasName = usable is not null,
            },
            cancellationToken: cancellationToken));
        if (id is { } returned)
        {
            return await GetAsync(returned, cancellationToken);
        }
        return await FindByEmailAsync(cleanEmail, cancellationToken)
            ?? throw new InvalidOperationException("The user row vanished between the upsert and the read.");
    }

    public async Task<IReadOnlyList<UserRef>> SearchMembersAsync(Guid projectId, string text, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var refs = await connection.QueryAsync<UserRef>(new CommandDefinition(
            $"""
            SELECT {UserRefs.Columns}
            FROM app_user u
            JOIN project_member m ON m.user_id = u.id AND m.project_id = @projectId AND m.archived_at IS NULL
            WHERE u.disabled_at IS NULL
              AND (starts_with(lower(u.name), lower(@text)) OR starts_with(lower(u.email), lower(@text)))
            ORDER BY lower(u.name) COLLATE "C", u.id
            """,
            new { projectId, text = (text ?? string.Empty).Trim() }, cancellationToken: cancellationToken));
        return refs.AsList();
    }
}
