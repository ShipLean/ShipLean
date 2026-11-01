namespace ShipLean.Contracts.Events;

// A retro point, theme or action link changed (every retro write publishes this after commit): open retro views for the
// listed sprints and work items refetch. Theme renames and merges send empty lists, which means refetch every retro view.
public sealed record RetroChanged(Guid ProjectId, IReadOnlyList<Guid> SprintIds, IReadOnlyList<Guid> WorkItemIds) : ProjectEvent(ProjectId);
