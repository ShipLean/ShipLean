using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;

// A teammate's edit arriving over the hub while the list is open. Its own class: the edit changes the item.
[TestFixture]
public class TeammateEditTests : SeededAcceptanceTest
{
    [Test]
    public async Task Backlog_WhenTeammateEdits_ShouldFlashRowAndToast()
    {
        var backlog = new StoriesBacklog(Page);
        var list = new BacklogInteractions(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var key = (await list.KeysAsync())[0];

        var tom = await BacklogWrites.SignInAsTomAsync(Page, BaseUrl);
        await BacklogWrites.SetPointsAsync(tom, key, 13);

        await Expect(list.Row(key)).ToHaveAttributeAsync("data-flash", "true");
        await Expect(list.Toasts.First).ToContainTextAsync($"Tom Okafor changed points on {key}");
        await Expect(list.Row(key).GetByTestId(BacklogTestIds.Points)).ToHaveTextAsync("13");
    }
}
