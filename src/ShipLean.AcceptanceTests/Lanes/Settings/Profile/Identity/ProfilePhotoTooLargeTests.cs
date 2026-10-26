using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Profile.Identity;

// A photo over 2 MB is refused with a message, and nothing is uploaded.
[TestFixture]
public class ProfilePhotoTooLargeTests : SeededAcceptanceTest
{
    [Test]
    public async Task Profile_WhenPhotoTooLarge_ShouldBeValidation()
    {
        var profile = new ProfileIdentityPage(Page);
        await profile.OpenAsync();
        var uploads = 0;
        Page.Request += (_, request) => uploads += request.Url.EndsWith("/api/me/photo", StringComparison.Ordinal) ? 1 : 0;

        await profile.PickPhotoAsync("big.png", new byte[(2 * 1024 * 1024) + 1]);

        await Expect(profile.PhotoError).ToContainTextAsync("over 2 MB");
        await Expect(profile.Avatar).ToHaveAttributeAsync("data-has-photo", "false");
        await Expect(profile.Save).ToBeDisabledAsync();
        uploads.ShouldBe(0);
    }
}
