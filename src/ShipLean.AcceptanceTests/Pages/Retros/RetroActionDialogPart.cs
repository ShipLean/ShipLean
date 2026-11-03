using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// The action dialog, opened from a note's Add action, a screen's Add action or a theme's action link.
public sealed class RetroActionDialogPart(IPage page)
{
    public ILocator Dialog => page.GetByTestId(RetroSharedTestIds.RetroActionDialog);

    public ILocator ModeNew => page.GetByTestId(RetroSharedTestIds.RetroActionModeNew);

    public ILocator ModeExisting => page.GetByTestId(RetroSharedTestIds.RetroActionModeExisting);

    public ILocator Title => page.GetByTestId(RetroSharedTestIds.RetroActionTitle);

    public ILocator Fix => page.GetByTestId(RetroSharedTestIds.RetroActionFix);

    public ILocator Theme => page.GetByTestId(RetroSharedTestIds.RetroActionTheme);

    public ILocator Existing => page.GetByTestId(RetroSharedTestIds.RetroActionExisting);

    // The picker of points to cover (selected ones are checked).
    public ILocator Pick => page.GetByTestId(RetroSharedTestIds.RetroActionPick);

    public ILocator FixOption(string label) => page.GetByTestId(RetroSharedTestIds.RetroActionFixOption).Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(label)}") });

    public ILocator PickCheck => page.GetByTestId(RetroSharedTestIds.RetroActionPickCheck);

    public ILocator Save => page.GetByTestId(RetroSharedTestIds.RetroActionSave);
}
