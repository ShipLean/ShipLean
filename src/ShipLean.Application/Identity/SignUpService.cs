using Agoda.IoC.Core;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ShipLean.Application.Features.Users;
using ShipLean.Contracts.Auth;
using ShipLean.Contracts.Errors;
using ShipLean.Core.People;

namespace ShipLean.Application.Identity;

// POST /api/auth/signup: anyone who can reach ShipLean creates a password user, while SHIPLEAN_SIGNUP_ENABLED is true
// (docs/auth.md). Meant to be on briefly, for example to make a store reviewer's account. Any value but true (any
// case) is off.
// TODO: unset counts as on, so prod (where the variable can't be set right now) gets sign-up for the Play reviewer's
// account. Replace with unset = off (IsEnabled reading only "true") in the follow-up revert PR on SHIP-1347.
[RegisterSingleton]
public sealed class SignUpService(NpgsqlDataSource dataSource, Passwords passwords, IConfiguration configuration, TimeProvider clock)
{
    public const string EnabledKey = "SHIPLEAN_SIGNUP_ENABLED";
    public const string Off = "Sign-up is turned off on this ShipLean.";
    public const string Taken = "An account with this email already exists. Sign in instead.";

    public bool IsEnabled => configuration[EnabledKey]?.Trim() is not { Length: > 0 } value
        || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    // The id of the new user, who is signed in by the caller. An existing email, in any case, is refused and never
    // adopted: taking over an account an admin added would need proof of the address, and sign-up has none.
    public async Task<Guid> SignUpAsync(SignUpRequest request, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            throw ApiException.NotFound(Off);
        }
        var name = UserInput.RequireName(request.Name);
        var email = UserInput.RequireEmail(request.Email);
        Passwords.RequireAcceptable(request.Password);
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var id = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            INSERT INTO app_user (id, email, name, initials, avatar_colour, security_stamp, password_hash, last_sign_in_at, created_at, updated_at)
            VALUES (@id, @email, @name, @initials, @colour, @stamp, @hash, @now, @now, @now)
            ON CONFLICT (lower(email)) DO NOTHING
            RETURNING id
            """,
            new
            {
                id = Guid.CreateVersion7(),
                email,
                name,
                initials = Initials.From(name),
                colour = AvatarPalette.For(email),
                stamp = AccountRules.NewStamp(),
                hash = passwords.Hash(request.Password!),
                now,
            },
            cancellationToken: cancellationToken));
        return id ?? throw ApiException.Conflict(Taken);
    }
}
