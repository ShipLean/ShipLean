namespace ShipLean.Contracts.Retros;

public sealed record RetroNotNeededDto(string Reason, RetroAuthorDto By, string DecidedAtLabel);
