namespace ShipLean.Contracts.DeviceSessions;

// POST /api/auth/device-sessions/codes: the app's S256 challenge (43 base64url characters) and its device name
// (1-100 characters after trimming).
public sealed record CreateSignInCodeRequest(string? Challenge, string? DeviceName);
