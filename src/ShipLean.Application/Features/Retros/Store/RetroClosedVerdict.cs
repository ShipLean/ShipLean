using ShipLean.Application.Features.Retros.Rules;

namespace ShipLean.Application.Features.Retros.Store;

// The verdict of one closed action item and the series index of the sprint it closed in.
public sealed record RetroClosedVerdict(Guid ItemId, DateTimeOffset CompletedAt, int ClosingIndex, RetroVerdictResult Result);
