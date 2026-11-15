using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Touch;

// The Feature's links and attachment rows on a phone against the real server (SHIP-714, L714f).
// The touch rules are checked on this task's regions only; the rest of the page is other tasks'.
[TestFixture]
public class FeatureLinksTouchTests : SeededAcceptanceTest
{
    // A loopback address is private: the server answers with the private message and never calls out.
    private const string PrivateUrl = "http://127.0.0.1:9/checkout/spec";

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Link_WhenAddedByUrlOnPhone_ShouldShowAsCardWithRemoveVisible()
    {
        var screen = new WorkItemScreen(Page);
        var add = new AddLinkForm(Page);
        var links = new LinksSectionPanel(Page);
        var rail = new AttachmentsRailPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-101");

        await add.Open.TapAsync();
        (await TouchRules.ViolationsAsync(Page, add.Form)).ShouldBeEmpty();
        await add.Url.FillAsync(PrivateUrl);
        await add.Url.BlurAsync();
        await Expect(add.Message).ToContainTextAsync("Private address");
        await add.Title.FillAsync("Checkout spec");
        await add.Submit.TapAsync();

        await Expect(add.Form).ToHaveCountAsync(0);
        var card = links.CardTitled("Checkout spec");
        await Expect(card).ToHaveCountAsync(1);
        await Expect(card.GetByTestId(FeatureTestIds.LinkCardClose)).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, links.Section)).ShouldBeEmpty();

        await rail.DropInput.SetInputFilesAsync(
            new FilePayload { Name = "notes.csv", MimeType = "text/csv", Buffer = "a,b\n1,2"u8.ToArray() });
        await Expect(rail.DropOverlay).ToBeVisibleAsync();
        await rail.DropUpload.TapAsync();
        await Expect(rail.Items).ToHaveCountAsync(1);
        await Expect(rail.RemoveOf("notes.csv")).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, rail.Items)).ShouldBeEmpty();
    }
}
