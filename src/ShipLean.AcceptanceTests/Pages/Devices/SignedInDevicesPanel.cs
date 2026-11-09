using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Devices;

// Signed-in devices on Your profile.
public sealed class SignedInDevicesPanel(IPage page)
{
    public ILocator Panel => page.GetByTestId(DevicesTestIds.Panel);

    public ILocator Rows => page.GetByTestId(DevicesTestIds.Row);

    public ILocator Row(string name) => Rows.Filter(new LocatorFilterOptions { HasTextRegex = new Regex(Regex.Escape(name)) });

    public async Task OpenAsync()
    {
        await page.GotoAsync(Links.Profile());
        await Assertions.Expect(page.GetByTestId(DevicesTestIds.SlotDevices)).ToBeVisibleAsync();
    }

    // Sign out on the row, then confirm.
    public async Task SignOutAsync(string name)
    {
        await Row(name).GetByTestId(DevicesTestIds.SignOut).ClickAsync();
        await page.GetByTestId(DevicesTestIds.ConfirmGo).ClickAsync();
    }
}
