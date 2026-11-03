using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;
using static ShipLean.AcceptanceTests.Lanes.Sprints.End.EndedSprint;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.End;

// Sprint 24 ended and not closed. Cancel changes nothing on the server, so the tests share one arrangement.
[TestFixture]
public class CloseSprintButtonTests : SeededAcceptanceTest
{
    [Test]
    public async Task CloseSprint_WhenASprintHasEnded_ShouldInterruptNobodyAndOfferTheButton()
    {
        await EndSprint24Async(ConnectionString);
        var keys = Keys(await ReadRolloverAsync(Page, SeedIds.Sprints.Sprint24));
        var prompt = new SprintEndPrompt(Page);

        // The places the prompt used to appear.
        await Page.GotoAsync(Links.Sprints("SHIP"));
        await Page.GetByTestId(SprintsTestIds.List).WaitForAsync();
        await Expect(prompt.Dialog).ToHaveCountAsync(0);

        await prompt.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await Expect(prompt.Dialog).ToHaveCountAsync(0);

        await prompt.PressCloseAsync();
        await Expect(prompt.Rows).ToHaveCountAsync(keys.Count);

        await prompt.Cancel.ClickAsync();

        await Expect(prompt.Dialog).ToHaveCountAsync(0);
        await Expect(prompt.CloseButton).ToBeVisibleAsync();
    }
}
