using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Retros;

public sealed record RetroCardDto(Guid Id, RetroItemRefDto? WorkItem, SprintRef? Sprint, RetroAuthorDto Author, bool Partial, DateTimeOffset CreatedAt, IReadOnlyList<RetroItemRefDto> CompiledFrom, IReadOnlyList<RetroPointDto> Points, RetroFailureLogDto FailureLog);
