using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// "Why this exists" on an action item's page: the points the action covers, each quoted with a link back to where it was written.
public sealed class ActionWhyPart(IPage page)
{
    public ILocator Section => page.GetByTestId(RetrosTestIds.RetroActionWhy);

    public ILocator Points => Section.GetByTestId(RetrosTestIds.RetroActionWhyPoint);

    public ILocator PointOf(string text) => Points.Filter(new() { HasText = text });

    public ILocator LinkOf(string text) => PointOf(text).GetByTestId(RetrosTestIds.RetroActionWhyLink);

    public ILocator Add => page.GetByTestId(RetrosTestIds.RetroActionWhyAdd);
}
