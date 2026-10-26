using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;

namespace ShipLean.AcceptanceTests.Lanes.Settings.States;

// Joel deletes Active from the User Story states. It has items, so he picks where they go; they move on Save. The class
// changes the project's states, so it has the seeded database to itself. The target starts on the design's default (the same category, then Proposed).
[TestFixture]
public class SettingsStateDeleteTests : SeededAcceptanceTest
{
    [Test]
    public async Task Settings_WhenStateDeletedWithItems_ShouldAskTargetAndMoveOnSave()
    {
        var states = new SettingsStatesPage(Page);
        await states.OpenAsync();
        await Expect(states.Rows).ToHaveCountAsync(5);
        await Expect(states.NameOf(1)).ToHaveValueAsync("Active");
        var active = int.Parse(await states.ItemsOf(1).InnerTextAsync());
        var fresh = int.Parse(await states.ItemsOf(0).InnerTextAsync());
        active.ShouldBeGreaterThan(0);

        await states.DeleteOf(1).ClickAsync();
        await Expect(states.DeleteForm).ToBeVisibleAsync();
        await Expect(states.DeleteConfirm).ToBeEnabledAsync();
        await states.ChooseTargetAsync("New");
        await states.DeleteConfirm.ClickAsync();
        await Expect(states.Rows).ToHaveCountAsync(4);
        await Expect(states.ItemsOf(0)).ToContainTextAsync($"+{active}");

        var response = await states.SaveAsync();
        response.Ok.ShouldBeTrue();
        await Expect(states.Unsaved).ToHaveCountAsync(0);

        await Page.ReloadAsync();
        await Expect(states.Rows).ToHaveCountAsync(4);
        await Expect(states.NameOf(0)).ToHaveValueAsync("New");
        await Expect(states.NameOf(1)).ToHaveValueAsync("Resolved");
        await Expect(states.ItemsOf(0)).ToHaveTextAsync((fresh + active).ToString());
    }
}
