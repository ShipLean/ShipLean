namespace ShipLean.Contracts.DeviceSessions;

// A successful exchange: the new session and its secret, which exists only here and in the response that carries it.
public sealed record DeviceSignIn(DeviceSessionRecord Session, string Secret);
