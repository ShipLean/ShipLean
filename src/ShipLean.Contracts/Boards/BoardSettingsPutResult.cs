namespace ShipLean.Contracts.Boards;

// Board is the merged, saved document. MergedSections names the sections (top-level property names) that another writer
// changed since the caller's BasedOn and the caller left alone: their version was kept.
public sealed record BoardSettingsPutResult(BoardSettingsDto Board, IReadOnlyList<string> MergedSections);
