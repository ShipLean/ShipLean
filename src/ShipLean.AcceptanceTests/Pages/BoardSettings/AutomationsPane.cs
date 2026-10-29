using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.BoardSettings;

// The board settings dialog's Automations pane (Stories and Features boards). Transitions are addressed by position.
public sealed class AutomationsPane(IPage page)
{
    public ILocator Pane => page.GetByTestId(BoardSettingsTestIds.AutomationsPane);

    public ILocator Rows => page.GetByTestId(BoardSettingsTestIds.TransitionRow);

    public ILocator Labels => page.GetByTestId(BoardSettingsTestIds.TransitionLabel);

    public ILocator Invalid => page.GetByTestId(BoardSettingsTestIds.TransitionInvalid);

    public ILocator Prompt(int transition) => Rows.Nth(transition).GetByTestId(BoardSettingsTestIds.ActionPrompt);

    public async Task OpenAsync(string projectKey, string level) =>
        await page.GotoAsync(Links.BoardSettings(projectKey, level, "automations"));

    public async Task AddAsync(string from, string to)
    {
        await page.GetByTestId(BoardSettingsTestIds.TransitionAddFrom).SelectOptionAsync(new SelectOptionValue { Label = from });
        await page.GetByTestId(BoardSettingsTestIds.TransitionAddTo).SelectOptionAsync(new SelectOptionValue { Label = to });
        await page.GetByTestId(BoardSettingsTestIds.TransitionAdd).ClickAsync();
    }

    public async Task SaveAsync(Guid boardId)
    {
        var saved = await page.RunAndWaitForApiAsync(
            $"/api/boards/{boardId}/settings", "PUT", () => page.GetByTestId(SettingsDialogTestIds.Save).ClickAsync());
        saved.Ok.ShouldBeTrue();
    }
}
