using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;

namespace ShipLean.AcceptanceTests.Lanes.Settings.NewProject;

// SHIP is Joel's own project: the check says so, offers free keys from the name, and an offered key passes.
[TestFixture]
public class NewProjectKeyTakenTests : SeededAcceptanceTest
{
    [Test]
    public async Task NewProject_WhenKeyTaken_ShouldOfferAlternatives()
    {
        var form = new NewProjectPage(Page);
        await form.OpenAsync();
        await form.Name.FillAsync("Ship Ahoy");

        await form.Key.FillAsync("SHIP");

        await Expect(form.KeyFree).ToContainTextAsync("SHIP is used by Storefront");
        await Expect(form.KeyFree).ToHaveAttributeAsync("data-ok", "false");
        await Expect(form.CreateButton).ToBeDisabledAsync();
        await Expect(form.Alternatives.First).ToBeVisibleAsync();
        var offered = await form.Alternatives.First.InnerTextAsync();
        offered.ShouldNotBe("SHIP");
        await form.Alternatives.First.ClickAsync();
        await Expect(form.Key).ToHaveValueAsync(offered);
        await Expect(form.KeyFree).ToHaveAttributeAsync("data-ok", "true");
        await Expect(form.CreateButton).ToHaveTextAsync($"Create {offered}");
    }
}
