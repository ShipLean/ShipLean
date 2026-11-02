namespace ShipLean.Contracts.Retros;

public sealed record RetroAggregateDto(RetroCategoryCountsDto Counts, int ToDecide, int Decided, int Decidable, int Actions);
