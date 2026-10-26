namespace ShipLean.Contracts.Projects;

public sealed record KeyCheckDto(string Key, bool Available, KeyCheckReason? Reason, IReadOnlyList<string> Alternatives);
