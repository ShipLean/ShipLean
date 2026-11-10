namespace ShipLean.Contracts.DeviceSessions;

// POST /api/auth/device-sessions/exchange: the code from the handoff and the verifier the challenge was made from.
public sealed record ExchangeSignInCodeRequest(string? Code, string? Verifier);
