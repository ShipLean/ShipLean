using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.MobileSignIn;

// The confirm page the phone app opens in the system browser (/mobile/sign-in).
public sealed class MobileSignInPage(IPage page)
{
    public ILocator Page => page.GetByTestId(MobileSignInTestIds.Page);

    public ILocator Card => page.GetByTestId(MobileSignInTestIds.Card);

    public ILocator SignIn => page.GetByTestId(MobileSignInTestIds.SignIn);

    public ILocator Cancel => page.GetByTestId(MobileSignInTestIds.Cancel);

    public ILocator Done => page.GetByTestId(MobileSignInTestIds.Done);

    public ILocator Cancelled => page.GetByTestId(MobileSignInTestIds.Cancelled);

    public ILocator OpenApp => page.GetByTestId(MobileSignInTestIds.OpenApp);

    public async Task OpenAsync(string challenge, string state, string device)
    {
        await page.GotoAsync("/mobile/sign-in" + Links.Query(("challenge", challenge), ("state", state), ("device", device)));
        await SignIn.WaitForAsync();
    }
}
