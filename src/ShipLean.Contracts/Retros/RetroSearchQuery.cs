namespace ShipLean.Contracts.Retros;

// The query string of the search route: q, sprint, category, theme, level, role, recurring, selected (a retro id).
public sealed record RetroSearchQuery(string? Q, Guid? Sprint, RetroCategory? Category, Guid? Theme, RetroLevel? Level, RetroRole? Role, bool? Recurring, Guid? Selected);
