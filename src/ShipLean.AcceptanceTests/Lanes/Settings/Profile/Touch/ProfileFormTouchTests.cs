using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Profile.Touch;

// Joel's profile on a phone (SHIP-733 D1, D2, D6): the identity form with the shared Save bar, the Password panel behind
// Change password, and Appearance. The touch rules are checked on this task's panels only.
[TestFixture]
public class ProfileFormTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private async Task<ProfileTouch> OpenAsync()
    {
        var profile = new ProfileTouch(Page);
        await profile.OpenAsync();
        return profile;
    }

    [Test]
    public async Task Profile_WhenEditedOnAPhone_ShouldSaveFromTheSaveBar()
    {
        var profile = await OpenAsync();
        var headings = await Page.EvaluateAsync<int>("[...document.querySelectorAll('h1, h2')].filter(h => h.textContent.trim() === 'Your profile').length");
        headings.ShouldBe(0);
        (await ProfileTouch.FontSizeAsync(profile.Name)).ShouldBe("16px");
        (await profile.Name.BoundingBoxAsync())!.Height.ShouldBe(36);
        await Expect(profile.SaveBar).ToHaveCountAsync(0);

        await profile.Name.FillAsync("Joel Dickson Jr");
        await profile.PurpleSwatch.TapAsync();

        await Expect(profile.SaveBarMessage).ToHaveTextAsync("Unsaved profile changes");
        await Expect(profile.PanelFooterSave).ToBeHiddenAsync();
        (await TouchRules.ViolationsAsync(Page, profile.IdentityPanel)).ShouldBeEmpty();
        await Page.RunAndWaitForApiAsync("/api/me/profile", "PUT", () => profile.SaveBarSave.TapAsync());
        await Expect(profile.SaveBar).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Profile_WhenTheNameIsCleared_ShouldSayEnterANameWithSaveOff()
    {
        var profile = await OpenAsync();

        await profile.Name.FillAsync("");

        await Expect(profile.SaveBarMessage).ToHaveTextAsync("Enter a name to save");
        await Expect(profile.SaveBarSave).ToBeDisabledAsync();
    }

    [Test]
    public async Task Password_WhenOpenedAndCancelledOnAPhone_ShouldClearAndClose()
    {
        var profile = await OpenAsync();
        await Expect(profile.PasswordForm).ToHaveCountAsync(0);
        (await TouchRules.ViolationsAsync(Page, profile.PasswordPanel)).ShouldBeEmpty();

        await profile.PasswordOpen.TapAsync();
        await Expect(profile.PasswordCurrent).ToBeVisibleAsync();
        await Expect(profile.PasswordNew).ToBeVisibleAsync();
        await Expect(profile.PasswordConfirm).ToBeVisibleAsync();
        await profile.PasswordCurrent.FillAsync("something-typed");
        await profile.PasswordNew.FillAsync("another-typed-one");
        await profile.PasswordConfirm.FillAsync("another-typed-one");
        (await TouchRules.ViolationsAsync(Page, profile.PasswordPanel)).ShouldBeEmpty();
        await profile.PasswordCancel.TapAsync();

        await Expect(profile.PasswordForm).ToHaveCountAsync(0);
        await profile.PasswordOpen.TapAsync();
        await Expect(profile.PasswordCurrent).ToHaveValueAsync("");
        await Expect(profile.PasswordNew).ToHaveValueAsync("");
        await Expect(profile.PasswordConfirm).ToHaveValueAsync("");
    }

    [Test]
    public async Task Password_WhenChangedOnAPhone_ShouldCloseWithTheToast()
    {
        var profile = await OpenAsync();
        await profile.PasswordOpen.TapAsync();
        await profile.PasswordCurrent.FillAsync(SeedIds.Passwords.Joel);
        await profile.PasswordNew.FillAsync("a-brand-new-password");
        await profile.PasswordConfirm.FillAsync("a-brand-new-password");

        await Page.RunAndWaitForApiAsync("/api/auth/password", "POST", () => profile.PasswordSubmit.TapAsync());

        await Expect(profile.PasswordForm).ToHaveCountAsync(0);
        await Expect(profile.Toasts.First).ToContainTextAsync("Password changed");
        await Expect(profile.PasswordOpen).ToBeVisibleAsync();
    }

    [Test]
    public async Task Appearance_WhenDensityPickedOnAPhone_ShouldApplyAtOnce()
    {
        var profile = await OpenAsync();
        (await TouchRules.ViolationsAsync(Page, profile.AppearancePanel)).ShouldBeEmpty();
        var widths = new List<double>();
        for (var i = 0; i < await profile.ThemeOptions.CountAsync(); i++)
        {
            var box = (await profile.ThemeOptions.Nth(i).BoundingBoxAsync())!;
            box.Height.ShouldBe(36);
            widths.Add(box.Width);
        }

        (widths.Max() - widths.Min()).ShouldBeLessThanOrEqualTo(1);
        ILocator? other = null;
        for (var i = 0; i < await profile.DensityOptions.CountAsync(); i++)
        {
            if (await profile.DensityOptions.Nth(i).GetAttributeAsync("aria-pressed") == "false")
            {
                other = profile.DensityOptions.Nth(i);
            }
        }

        other.ShouldNotBeNull();
        var value = await other.GetAttributeAsync("data-value");

        await Page.RunAndWaitForApiAsync("/api/me/preferences/density", "PUT", () => other.TapAsync());

        await Page.WaitForFunctionAsync("v => document.documentElement.dataset.density === v", value);
    }
}
