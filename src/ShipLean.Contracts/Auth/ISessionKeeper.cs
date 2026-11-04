namespace ShipLean.Contracts.Auth;

// Keeps this request's sign-in alive after the user's security stamp has been rotated, which ends every session. The
// stamp changes in the Users feature (an admin resets their own password or email); the cookie belongs to Auth.
public interface ISessionKeeper
{
    // Replaces this request's cookie with one under the user's current stamp, so only their other sessions end.
    Task KeepAsync(Guid userId, CancellationToken cancellationToken);
}
