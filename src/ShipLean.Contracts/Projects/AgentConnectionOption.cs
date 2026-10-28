namespace ShipLean.Contracts.Projects;

// A connection a project admin can choose on the General tab: its name only. Archived is true only for the project's
// current choice after it was archived (shown, not pickable).
public sealed record AgentConnectionOption(Guid Id, string Name, bool Archived);
