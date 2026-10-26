using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Shell;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Shell.Chrome;

[TestFixture]
public class SignOutTests : SeededAcceptanceTest
{
    [Test]
    public async Task UserMenu_WhenSignOut_ShouldOpenSignInSignedOut()
    {
        var frame = new ShellFrame(Page);
        await frame.OpenAsync(ShellFrame.BacklogPath("SHIP"));

        await frame.SignOutAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/login\?state=signed-out$"));
        await Expect(Page.GetByTestId(AppTestIds.LoginPage)).ToBeVisibleAsync();
    }
}
