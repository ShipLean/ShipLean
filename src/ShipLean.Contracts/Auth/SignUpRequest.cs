namespace ShipLean.Contracts.Auth;

// POST /api/auth/signup.
public sealed record SignUpRequest(string? Name, string? Email, string? Password);
