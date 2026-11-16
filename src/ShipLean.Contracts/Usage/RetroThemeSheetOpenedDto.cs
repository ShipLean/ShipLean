namespace ShipLean.Contracts.Usage;

// POST api/projects/{id}/usage/retro-theme-sheet-opened: the SPA reports a sheet opened on the retro insights Themes tab at touch.
// Sheet is which one: theme (one theme's sheet) or unthemed (the points with no theme).
public sealed record RetroThemeSheetOpenedDto(string Sheet);
