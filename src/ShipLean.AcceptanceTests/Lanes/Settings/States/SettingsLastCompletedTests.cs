using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;

namespace ShipLean.AcceptanceTests.Lanes.Settings.States;

// Closed is the only Completed state of a User Story, so it can't be deleted. Nothing here changes data.
[TestFixture]
public class SettingsLastCompletedTests : SeededAcceptanceTest
{
    [Test]
    public async Task Settings_WhenLastCompletedState_ShouldNotDelete()
    {
        var states = new SettingsStatesPage(Page);
        await states.OpenAsync();

        await Expect(states.NameOf(3)).ToHaveValueAsync("Closed");
        await Expect(states.CategoryOf(3)).ToHaveValueAsync("completed");
        await Expect(states.DeleteOf(3)).ToBeDisabledAsync();
        await Expect(states.DeleteBlockOf(3)).ToContainTextAsync("last Completed");
        await Expect(states.DeleteOf(1)).ToBeEnabledAsync();
    }
}
