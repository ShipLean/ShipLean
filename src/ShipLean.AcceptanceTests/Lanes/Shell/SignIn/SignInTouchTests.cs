using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Auth;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Shell.SignIn;

// Sign in and Choose your own password on a phone against the real server (SHIP-736 L736a). The touch rules are checked
// on the sign-in card only; the route sweep checks the whole page.
[TestFixture]
public class SignInTouchTests : SeededAcceptanceTest
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private ILocator Locator(string testId) => Page.GetByTestId(testId);

    private async Task<LocatorBox> BoxAsync(string testId)
    {
        var box = await Locator(testId).BoundingBoxAsync();
        box.ShouldNotBeNull($"{testId} has no box");
        return new LocatorBox(box.X, box.Y, box.Width, box.Height);
    }

    private sealed record LocatorBox(float X, float Y, float Width, float Height)
    {
        public float Bottom => Y + Height;
    }

    [Test]
    public async Task SignIn_WhenOpenedOnAPhone_ShouldPutTheCardUnderTheBarWithoutFocus()
    {
        var login = new LoginScreen(Page);
        await login.OpenAsync();

        var bar = await BoxAsync(AuthTestIds.TopBar);
        var card = await BoxAsync(AuthTestIds.SignInForm);
        bar.Height.ShouldBe(48f, 1f);
        (card.Y - bar.Bottom).ShouldBe(16f, 1f);
        card.Width.ShouldBe(358f, 1f);
        await Expect(Locator(AuthTestIds.Email)).Not.ToBeFocusedAsync();
        foreach (var id in new[] { AuthTestIds.Email, AuthTestIds.Password })
        {
            (await BoxAsync(id)).Height.ShouldBe(40f, 1f);
            (await Locator(id).EvaluateAsync<string>("el => getComputedStyle(el).fontSize")).ShouldBe("16px");
        }

        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth"))
            .ShouldBeTrue();
        (await TouchRules.ViolationsAsync(Page, login.SignInForm)).ShouldBeEmpty();
    }

    [Test]
    public async Task SignIn_WhenTheKeyboardCoversTheBottom_ShouldKeepTheFormVisible()
    {
        var login = new LoginScreen(Page);
        await login.OpenAsync();
        await Page.SetViewportSizeAsync(390, 508);

        foreach (var id in new[] { AuthTestIds.Email, AuthTestIds.Password, AuthTestIds.SignInSubmit })
        {
            var box = await BoxAsync(id);
            box.Y.ShouldBeGreaterThanOrEqualTo(0f);
            box.Bottom.ShouldBeLessThanOrEqualTo(508f, $"{id} is below the keyboard-covered viewport");
        }
    }

    [Test]
    public async Task SignIn_WhenOnAPhone_ShouldSignInWithTheFullWidthButton()
    {
        var login = new LoginScreen(Page);
        await login.OpenAsync();

        // The card is 358px wide with a 1px border and 16px padding each side.
        var button = await BoxAsync(AuthTestIds.SignInSubmit);
        var card = await BoxAsync(AuthTestIds.SignInForm);
        button.Width.ShouldBe(card.Width - 34f, 1f);
        button.Height.ShouldBe(40f, 1f);

        await login.SignInAsync("joel@shiplean.local", SeedIds.Passwords.Joel);

        await Expect(Page).Not.ToHaveURLAsync(new Regex("/login"));
        var me = await Page.Context.APIRequest.GetAsync("/api/me");
        me.Status.ShouldBe(200);
    }

    [Test]
    public async Task ChooseOwnPassword_WhenOnAPhone_ShouldStackChecksAndSaveAndContinue()
    {
        var login = new LoginScreen(Page);
        await login.OpenAsync();
        await login.SignInAsync("ravi@shiplean.local", SeedIds.Passwords.Ravi);
        await Expect(login.ChangeForm).ToBeVisibleAsync();
        await Expect(Locator(AuthTestIds.NewPassword)).Not.ToBeFocusedAsync();

        var confirm = await BoxAsync(AuthTestIds.ConfirmPassword);
        var checks = await BoxAsync(AuthTestIds.PasswordChecks);
        var save = await BoxAsync(AuthTestIds.ChangeSubmit);
        var signOut = await BoxAsync(AuthTestIds.SignOut);
        checks.X.ShouldBe(confirm.X, 1f);
        checks.Y.ShouldBeGreaterThanOrEqualTo(confirm.Bottom);
        save.Y.ShouldBeGreaterThanOrEqualTo(checks.Bottom);
        save.Width.ShouldBe(confirm.Width, 1f);
        save.Height.ShouldBe(40f, 1f);
        signOut.Y.ShouldBeGreaterThan(save.Bottom);
        signOut.Height.ShouldBeGreaterThanOrEqualTo(32f);
        (await TouchRules.ViolationsAsync(Page, login.ChangeForm)).ShouldBeEmpty();

        await login.ChooseNewPasswordAsync("a-password-only-ravi-knows");

        await Expect(Page).Not.ToHaveURLAsync(new Regex("/login"));
        var me = await Page.Context.APIRequest.GetAsync("/api/me");
        me.Status.ShouldBe(200);
    }
}
