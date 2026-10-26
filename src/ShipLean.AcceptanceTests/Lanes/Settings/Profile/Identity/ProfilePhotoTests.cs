using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Profile.Identity;

// Joel uploads a photo, it is served at his photoUrl to the browser, and removing it takes it away again.
[TestFixture]
public class ProfilePhotoTests : SeededAcceptanceTest
{
    [Test]
    public async Task Profile_WhenPhotoUploaded_ShouldServeAtPhotoUrl()
    {
        var profile = new ProfileIdentityPage(Page);
        await profile.OpenAsync();

        await profile.PickPhotoAsync("me.png", ProfileIdentityPage.NewPng());
        await Expect(profile.Avatar).ToHaveAttributeAsync("data-has-photo", "true");
        await profile.SaveAsync("PUT", "/api/me/photo");

        var photo = profile.AvatarPhoto;
        await Expect(photo).ToHaveAttributeAsync("src", new System.Text.RegularExpressions.Regex(@"^/api/users/[0-9a-f-]{36}/photo\?v=1$"));
        var src = (await photo.GetAttributeAsync("src"))!;
        var served = await Page.EvaluateAsync<string>("async url => { const r = await fetch(url); return r.status + ' ' + r.headers.get('content-type'); }", src);
        served.ShouldBe("200 image/png");
        await Expect(photo).ToHaveJSPropertyAsync("complete", true);

        await Page.GetByTestId(SettingsTestIds.ProfilePhotoRemove).ClickAsync();
        await profile.SaveAsync("DELETE", "/api/me/photo");

        await Expect(profile.Avatar).ToHaveAttributeAsync("data-has-photo", "false");
        // The browser keeps a versioned photo for a year, so ask the server itself.
        var gone = await Page.EvaluateAsync<int>("async url => (await fetch(url, { cache: 'reload' })).status", src);
        gone.ShouldBe(404);
    }
}
