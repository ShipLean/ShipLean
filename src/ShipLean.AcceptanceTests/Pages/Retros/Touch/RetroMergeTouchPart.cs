using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros.Touch;

// Merge themes, proposed merges, Unthemed and What keeps working at 900px and below (SHIP-731 D8): the full-screen merge
// opened from the address, a proposal's stacked answers, the Unthemed card and the clamped latest mention.
public sealed class RetroMergeTouchPart(IPage page)
{
    public ILocator Dialog => page.GetByTestId(RetrosTestIds.RetroMergeDialog);

    public ILocator Close => page.GetByTestId(TouchTestIds.DialogClose);

    public ILocator Primary => page.GetByTestId(TouchTestIds.DialogPrimary);

    public ILocator Save => page.GetByTestId(RetrosTestIds.RetroMergeSave);

    public ILocator Sources => page.GetByTestId(RetrosTestIds.RetroMergeSource);

    // The checkbox of the theme with this id: the one whose data-theme-id matches, found by position.
    public async Task<ILocator> SourceOfAsync(Guid themeId)
    {
        await Sources.First.WaitForAsync();
        var ids = await Sources.EvaluateAllAsync<string[]>("boxes => boxes.map(b => b.getAttribute('data-theme-id'))");
        return Sources.Nth(Array.IndexOf(ids, themeId.ToString()));
    }

    public ILocator Target => page.GetByTestId(RetrosTestIds.RetroMergeTarget);

    public ILocator ThemeCards => page.GetByTestId(RetrosTestIds.RetroThemeCard);

    public ILocator Unthemed => page.GetByTestId(RetrosTestIds.RetroInsightsUnthemed);

    public ILocator KeepsWorkingLatest => page.GetByTestId(RetrosTestIds.RetroKeepsWorkingLatest);

    public bool AddressHasMerge => Regex.IsMatch(page.Url, "[?&]merge=");
}
