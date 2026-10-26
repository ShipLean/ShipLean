namespace ShipLean.Contracts.Users;

// The password an admin passes on. The server never stores it in clear or shows it again; the user must change it at
// first sign-in.
public sealed record OneTimePasswordDto(Guid UserId, string Password);
