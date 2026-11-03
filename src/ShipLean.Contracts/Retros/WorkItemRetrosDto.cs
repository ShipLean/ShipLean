namespace ShipLean.Contracts.Retros;

public sealed record WorkItemRetrosDto(IReadOnlyList<RetroCardDto> Cards, IReadOnlyList<WorkItemRetroRowDto> Rows);
