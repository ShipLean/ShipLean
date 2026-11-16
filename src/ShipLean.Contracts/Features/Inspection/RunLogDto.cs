namespace ShipLean.Contracts.Inspection;

public sealed record RunLogDto(string TimeLabel, string Severity, string Body, IReadOnlyList<RunAttributeDto> Attributes);
