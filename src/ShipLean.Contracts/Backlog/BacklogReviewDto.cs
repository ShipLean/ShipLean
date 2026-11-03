namespace ShipLean.Contracts.Backlog;

// The five review_backlog checks over the top of the unfiltered stack: Top rows, stale after StaleDays. Always all five
// checks, in BacklogCheckIds.All order.
public sealed record BacklogReviewDto(int Top, int StaleDays, IReadOnlyList<BacklogReviewCheckDto> Checks);
