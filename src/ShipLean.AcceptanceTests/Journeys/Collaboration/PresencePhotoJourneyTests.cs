using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.Collaboration;

// Tom sets a profile photo, then opens the board Joel is on: Joel sees Tom's photo in the presence stack, and Tom
// leaving takes him out of it.
[TestFixture]
public class PresencePhotoJourneyTests : SeededAcceptanceTest
{
    [Test]
    public async Task J6_Presence_TeammatePhotoAppears()
    {
        var board = new BoardScreen(Page);
        await board.OpenAsync("SHIP", "stories");
        await Expect(board.View).ToBeVisibleAsync();
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var stack = Page.GetByTestId(RealtimeTestIds.PresenceStack);
        await Expect(stack).ToHaveCountAsync(0);

        await using var tom = await PresenceViewer.SignInAsTomAsync(Page, BaseUrl);
        var profile = new ProfileIdentityPage(tom.Page);
        await profile.OpenAsync();
        await profile.PickPhotoAsync("tom.png", ProfileIdentityPage.NewPng());
        await profile.SaveAsync("PUT", "/api/me/photo");
        await Expect(profile.AvatarPhoto).ToHaveAttributeAsync("src", new Regex(@"^/api/users/[0-9a-f-]{36}/photo\?v=\d+$"));
        var photoSrc = (await profile.AvatarPhoto.GetAttributeAsync("src"))!;

        await new BoardScreen(tom.Page).OpenAsync("SHIP", "stories");

        var avatars = stack.GetByTestId(ComponentTestIds.AvatarPhoto);
        await Expect(Page.GetByTestId(RealtimeTestIds.PresenceAvatar)).ToHaveCountAsync(1);
        await Expect(avatars).ToHaveAttributeAsync("src", new Regex("^" + Regex.Escape(photoSrc) + "$"));

        await tom.Page.GotoAsync(Links.Profile());

        await Expect(stack).ToHaveCountAsync(0);
    }
}
