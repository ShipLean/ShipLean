using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Inspection;

// SHIP-744 AC11: a run whose item was served no prompts shows no Prompts line. Its own class, so its database has no servings.
[TestFixture]
public class RunWithoutPromptsJourneyTests : ShipLean.AcceptanceTests.Inspection.InspectionAcceptanceTest
{
    [Test]
    public async Task Run_WhenNoPromptsServed_ShouldHideThePromptsLine()
    {
        await Page.GotoAsync($"{BaseUrl}/p/{ProjectKey}/items/{ItemKey}/runs/sess-cc-1");
        await Expect(Page.GetByTestId(InspectionTestIds.RunHeader)).ToBeVisibleAsync();

        await Expect(Page.GetByTestId(InspectionTestIds.RunPrompts)).ToHaveCountAsync(0);
    }
}
