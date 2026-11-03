namespace ShipLean.Contracts.Retros;

public sealed record WorkItemRetroRowDto(Guid RetroId, RetroAuthorDto Author, bool Partial, string CreatedAtLabel, RetroAggregateDto Totals);
