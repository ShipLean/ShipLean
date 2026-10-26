using Dapper;
using Npgsql;
using ShipLean.Application.Identity;

namespace ShipLean.TestSupport.Builders;

// Arranges one app_user with parameterised SQL and returns its id. Defaults give an active, non-admin user with the
// password ArrangedPassword, a unique email and a fresh security stamp.
public sealed class AppUserBuilder
{
    // The plaintext of every arranged password unless the test says otherwise.
    public const string ArrangedPassword = "arranged-password-1";

    private static readonly Passwords Hasher = new();
    private static int _counter;

    private readonly int _number = Interlocked.Increment(ref _counter);
    private Guid _id = Guid.NewGuid();
    private string? _email;
    private string? _name;
    private string? _password = ArrangedPassword;
    private bool _mustChangePassword;
    private DateTimeOffset? _lockedUntil;
    private DateTimeOffset? _disabledAt;
    private bool _isAdmin;
    private bool _hasPhoto;

    public AppUserBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public AppUserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public AppUserBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    // Null arranges a user with no password (status noPassword).
    public AppUserBuilder WithPassword(string? password)
    {
        _password = password;
        return this;
    }

    public AppUserBuilder MustChangePassword()
    {
        _mustChangePassword = true;
        return this;
    }

    public AppUserBuilder LockedUntil(DateTimeOffset lockedUntil)
    {
        _lockedUntil = lockedUntil;
        return this;
    }

    public AppUserBuilder DisabledAt(DateTimeOffset disabledAt)
    {
        _disabledAt = disabledAt;
        return this;
    }

    public AppUserBuilder Admin()
    {
        _isAdmin = true;
        return this;
    }

    // Sets avatar_photo_blob and photo_version 1, so photoUrl is the user's photo path with ?v=1.
    public AppUserBuilder WithPhoto()
    {
        _hasPhoto = true;
        return this;
    }

    public string Email => _email ?? $"arranged-{_number}@example.test";

    public string Name => _name ?? $"Arranged Person {_number}";

    public async Task<Guid> InsertAsync(NpgsqlConnection connection)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO app_user (id, email, name, initials, avatar_colour, avatar_photo_blob, photo_version, password_hash, must_change_password,
                                  security_stamp, locked_until, is_admin, disabled_at)
            VALUES (@id, @email, @name, @initials, '#2f7fd8', @photo, @photoVersion, @hash, @mustChange, @stamp, @lockedUntil, @isAdmin, @disabledAt)
            """,
            new
            {
                id = _id,
                email = Email,
                name = Name,
                initials = Initials(Name),
                photo = _hasPhoto ? "blob-" + _id : null,
                photoVersion = _hasPhoto ? 1 : 0,
                hash = _password is null ? null : Hasher.Hash(_password),
                mustChange = _mustChangePassword,
                stamp = "arranged-stamp-" + _id.ToString("N"),
                lockedUntil = _lockedUntil,
                isAdmin = _isAdmin,
                disabledAt = _disabledAt,
            });
        return _id;
    }

    private static string Initials(string name)
    {
        var letters = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => word[0]).Where(char.IsLetter).ToArray();
        if (letters.Length == 0)
        {
            return "A";
        }
        return string.Concat(letters[0], letters[^1] == letters[0] && letters.Length == 1 ? string.Empty : letters[^1].ToString()).ToUpperInvariant();
    }
}
