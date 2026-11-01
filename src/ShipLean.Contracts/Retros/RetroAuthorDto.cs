using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Retros;

public sealed record RetroAuthorDto(UserRef User, string? AgentName, RetroRole Role);
