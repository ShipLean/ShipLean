namespace ShipLean.Contracts.Events;

// Sections are the top-level property names of the board settings document that changed.
public sealed record BoardSettingsChanged(Guid ProjectId, Guid BoardId, IReadOnlyList<string> Sections) : ProjectEvent(ProjectId);
