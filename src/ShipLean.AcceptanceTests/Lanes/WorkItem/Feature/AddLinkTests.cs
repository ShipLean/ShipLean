using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Feature;

[TestFixture]
public class AddLinkTests : SeededAcceptanceTest
{
    // A loopback address is private: the server answers with the private message and never calls out.
    private const string PrivateUrl = "http://127.0.0.1:9/checkout/spec";

    [Test]
    public async Task AddLink_WhenUrlAdded_ShouldSaveWithoutFormSave()
    {
        var screen = new WorkItemScreen(Page);
        var add = new AddLinkForm(Page);
        await screen.OpenAsync("SHIP", "SHIP-101");
        var before = await RevisionAsync();

        await add.Open.ClickAsync();
        await add.Url.FillAsync(PrivateUrl);
        await add.Url.BlurAsync();
        await Expect(add.Message).ToContainTextAsync("Private address");
        await add.Title.FillAsync("Checkout spec");
        await add.Submit.ClickAsync();

        await Expect(add.Form).ToHaveCountAsync(0);
        await Expect(new LinksSectionPanel(Page).CardTitled("Checkout spec")).ToHaveCountAsync(1);
        var cards = await Page.Context.APIRequest.GetAsync($"/api/work-items/{SeedIds.WorkItems.Ship101}/link-cards");
        cards.Ok.ShouldBeTrue(await cards.TextAsync());
        (await cards.TextAsync()).ShouldContain(PrivateUrl);
        (await RevisionAsync()).ShouldBe(before);
        await Expect(screen.Save).ToBeDisabledAsync();
    }

    private async Task<int> RevisionAsync()
    {
        var response = await Page.Context.APIRequest.GetAsync($"/api/work-items/{SeedIds.WorkItems.Ship101}");
        response.Ok.ShouldBeTrue(await response.TextAsync());
        using var json = JsonDocument.Parse(await response.TextAsync());
        return json.RootElement.GetProperty("revision").GetInt32();
    }
}
