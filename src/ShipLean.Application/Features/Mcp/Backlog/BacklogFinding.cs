namespace ShipLean.Application.Features.Mcp.Backlog;

// One backlog row with what is wrong with it. Flags are unestimated, no_acceptance_criteria, no_parent, stale and
// likely_duplicate; DaysSinceChange is whole days; Duplicates are the keys of the rows it looks like.
public sealed record BacklogFinding(
    string Key, string Title, string Type, string State, decimal? Points, int DaysSinceChange,
    IReadOnlyList<string> Flags, IReadOnlyList<string> Duplicates);
