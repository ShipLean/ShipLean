using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// The four-column board of retro notes, with its Add a point form. Built on the locator of the area that holds it.
public sealed class RetroBoardPart(ILocator scope)
{
    public ILocator Board => scope.GetByTestId(RetroSharedTestIds.RetroBoard);

    public ILocator Columns => scope.GetByTestId(RetroSharedTestIds.RetroBoardColumn);

    public ILocator Notes => scope.GetByTestId(RetroSharedTestIds.RetroNote);

    public RetroNotePart NoteOf(string text) => new(Notes.Filter(new() { HasText = text }));

    public ILocator Add => scope.GetByTestId(RetroSharedTestIds.RetroBoardAdd);

    public ILocator AddText => scope.GetByTestId(RetroSharedTestIds.RetroBoardAddText);

    public ILocator AddCause => scope.GetByTestId(RetroSharedTestIds.RetroBoardAddCause);

    public ILocator AddAbout => scope.GetByTestId(RetroSharedTestIds.RetroBoardAddAbout);

    public ILocator AddSave => scope.GetByTestId(RetroSharedTestIds.RetroBoardAddSave);

    public ILocator AddCancel => scope.GetByTestId(RetroSharedTestIds.RetroBoardAddCancel);
}
