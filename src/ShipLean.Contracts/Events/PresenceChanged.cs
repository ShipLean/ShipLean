namespace ShipLean.Contracts.Events;

// ScreenId is a client route id. UserIds are everyone currently on that screen. Sequence grows with every presence
// change: of two lists for one screen, the one with the higher sequence is newer (they can arrive out of order when
// several instances publish them).
public sealed record PresenceChanged(Guid ProjectId, string ScreenId, IReadOnlyList<Guid> UserIds, long Sequence) : ProjectEvent(ProjectId);
