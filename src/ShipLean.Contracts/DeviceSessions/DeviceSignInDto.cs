using ShipLean.Contracts.Users;

namespace ShipLean.Contracts.DeviceSessions;

// 201 from the exchange. Secret is shown once: only its hash is stored.
public sealed record DeviceSignInDto(DeviceSessionDto Session, string Secret, MeDto Me);
