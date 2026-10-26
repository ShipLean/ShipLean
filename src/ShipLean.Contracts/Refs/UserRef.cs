namespace ShipLean.Contracts.Refs;

public sealed record UserRef(Guid Id, string Name, string Initials, string AvatarColour, string? PhotoUrl);
