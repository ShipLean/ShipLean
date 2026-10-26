using Microsoft.Playwright;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Shell;
using ShipLean.Contracts;

namespace ShipLean.AcceptanceTests.Lanes.Shell.Chrome;

[TestFixture]
public class AppearanceTests : SeededAcceptanceTest
{
    [Test]
    public async Task Shell_WhenDarkPreferenceSaved_ShouldOpenDark()
    {
        var saved = await Page.Context.APIRequest.PutAsync("/api/me/preferences/theme", new APIRequestContextOptions
        {
            DataObject = "dark",
            Headers = new Dictionary<string, string> { [ClientHeader.Name] = "acceptance-test" },
        });
        saved.Status.ShouldBe(204);
        var frame = new ShellFrame(Page);

        await frame.OpenAsync(ShellFrame.BacklogPath("SHIP"));

        await Page.WaitForFunctionAsync("document.documentElement.dataset.theme === 'dark'");
    }
}
