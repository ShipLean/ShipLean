using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// One retro note, wherever it is shown (a board, the search, the dialog's lists). Built on a locator, so a screen's page
// object hands out the note it wants: `board.NoteOf("text")`.
public sealed class RetroNotePart(ILocator root)
{
    public ILocator Root => root;

    public ILocator AddAction => root.GetByTestId(RetroSharedTestIds.RetroNoteAddAction);

    public ILocator NotNeeded => root.GetByTestId(RetroSharedTestIds.RetroNoteNotNeeded);

    public ILocator WhyText => root.GetByTestId(RetroSharedTestIds.RetroNoteWhyText);

    public ILocator WhySave => root.GetByTestId(RetroSharedTestIds.RetroNoteWhySave);

    public ILocator WhyCancel => root.GetByTestId(RetroSharedTestIds.RetroNoteWhyCancel);

    // The action pin (the note's link to its action item).
    public ILocator Pin => root.GetByTestId(RetroSharedTestIds.RetroNotePin);

    // The "No action needed" line.
    public ILocator None => root.GetByTestId(RetroSharedTestIds.RetroNoteNone);

    public ILocator Undo => root.GetByTestId(RetroSharedTestIds.RetroNoteUndo);

    public ILocator Theme => root.GetByTestId(RetroSharedTestIds.RetroNoteTheme);

    public ILocator More => root.GetByTestId(RetroSharedTestIds.RetroNoteMore);

    public ILocator Archive => root.GetByTestId(RetroSharedTestIds.RetroNoteArchive);

    // True when the note is the one the address asked to highlight (point=).
    public async Task<bool> IsHighlightedAsync() => await root.GetAttributeAsync("data-highlighted") is not null;
}
