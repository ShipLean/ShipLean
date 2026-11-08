using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// A retro note at 900px and below (SHIP-727 D5, D6, D8): the note itself, the sheets its ⋯ and # theme open, and the
// full-screen New action item its "+ Action item" opens. The sheets and the dialog sit outside the note, on the page.
public sealed class RetroTouchNotePart(IPage page, ILocator root)
{
    public RetroNotePart Note { get; } = new(root);

    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator SheetTitle => page.GetByTestId(TouchTestIds.SheetTitle);

    // A row of the action sheet the note's ⋯ opens (Move to theme, Archive).
    public ILocator SheetRow(string label) => page.GetByTestId(TouchTestIds.ActionSheetRow).Filter(new() { HasText = label });

    // The theme picker inside the Move to theme sheet.
    public ILocator ThemePicker => Sheet.GetByTestId(RetroSharedTestIds.RetroThemePicker);

    // The full-screen dialog's ✕ at the top left (K5).
    public ILocator DialogClose => page.GetByTestId(TouchTestIds.DialogClose);

    // The full-screen dialog's top-right slot, holding Create (or Add).
    public ILocator DialogPrimary => page.GetByTestId(TouchTestIds.DialogPrimary);

    public ILocator Create => page.GetByTestId(RetroSharedTestIds.RetroActionCreate);

    // "Starts in New with no sprint…", at the end of the dialog's body.
    public ILocator StartsNote => page.GetByTestId(RetroSharedTestIds.RetroActionStartsNote);
}
