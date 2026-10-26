using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Feature;

[TestFixture]
public class LinksSectionTests : SeededAcceptanceTest
{
    [Test]
    public async Task LinksSection_WhenUrlLinkAdded_ShouldShowCard()
    {
        var links = new LinksSectionPanel(Page);
        await new WorkItemScreen(Page).OpenAsync("SHIP", "SHIP-101");
        await Expect(links.Section).ToBeVisibleAsync();

        var added = await Page.Context.APIRequest.PostAsync($"/api/work-items/{SeedIds.WorkItems.Ship101}/link-cards", new APIRequestContextOptions
        {
            DataObject = new { url = "https://wiki.example.com/checkout/rollout-checklist", title = "Rollout checklist" },
        });
        added.Ok.ShouldBeTrue(await added.TextAsync());

        var card = links.CardTitled("Rollout checklist");
        await Expect(card).ToHaveCountAsync(1);
        await Expect(card.GetByTestId(FeatureTestIds.LinkCardTitle)).ToHaveAttributeAsync("href", "https://wiki.example.com/checkout/rollout-checklist");
    }
}
