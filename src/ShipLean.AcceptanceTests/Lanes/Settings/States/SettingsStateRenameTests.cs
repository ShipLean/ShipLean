using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;

namespace ShipLean.AcceptanceTests.Lanes.Settings.States;

// Joel renames Active; it stays In progress, so its colour is unchanged. The class changes the project's states, so it
// has the seeded database to itself.
[TestFixture]
public class SettingsStateRenameTests : SeededAcceptanceTest
{
    [Test]
    public async Task Settings_WhenStateRenamed_ShouldKeepCategoryColour()
    {
        var states = new SettingsStatesPage(Page);
        await states.OpenAsync();
        await Expect(states.NameOf(1)).ToHaveValueAsync("Active");
        await Expect(states.DotOf(1)).ToHaveAttributeAsync("data-category", "inProgress");

        await states.NameOf(1).FillAsync("Doing");
        await Expect(states.DotOf(1)).ToHaveAttributeAsync("data-category", "inProgress");
        var response = await states.SaveAsync();
        response.Ok.ShouldBeTrue();
        await Expect(states.Unsaved).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        await Expect(states.NameOf(1)).ToHaveValueAsync("Doing");
        await Expect(states.CategoryOf(1)).ToHaveValueAsync("inProgress");
        await Expect(states.DotOf(1)).ToHaveAttributeAsync("data-category", "inProgress");
    }
}
