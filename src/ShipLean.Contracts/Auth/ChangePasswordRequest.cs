namespace ShipLean.Contracts.Auth;

// POST /api/auth/password: change your own password.
public sealed record ChangePasswordRequest(string? Current, string? New);
