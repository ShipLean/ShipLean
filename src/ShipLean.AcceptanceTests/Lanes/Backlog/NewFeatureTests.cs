using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog;

// + New Feature on the seeded Storefront project (SHIP), signed in as Joel. This class creates one Feature.
[TestFixture]
public class NewFeatureTests : SeededAcceptanceTest
{
    [Test]
    public async Task NewFeature_WhenCreated_ShouldOpenFeatureWithGoalStatusAndSlug()
    {
        var backlog = new LevelBacklog(Page);
        await backlog.OpenAsync("SHIP", "features");
        var dialog = new NewFeatureDialog(Page);
        await dialog.OpenAsync();

        await dialog.Title.FillAsync("Express checkout");
        await Expect(dialog.Slug).ToHaveValueAsync("express-checkout");
        await Expect(dialog.SlugNote).ToHaveAttributeAsync("data-available", "true");
        await dialog.Goal.FillAsync("Returning customers pay without leaving the product page.");
        await dialog.StartAs("Active").ClickAsync();
        await dialog.Status.FillAsync("Nothing built yet.");
        await dialog.Create.ClickAsync();

        await Expect(Page.GetByTestId(FeatureTestIds.Form)).ToBeVisibleAsync();
        await Expect(Page.GetByTestId(FeatureTestIds.GoalView)).ToHaveTextAsync("Returning customers pay without leaving the product page.");
        await Expect(Page.GetByTestId(FeatureTestIds.StatusLine)).ToHaveTextAsync("Nothing built yet.");
        await Expect(Page.GetByTestId(FeatureTestIds.StatusState)).ToContainTextAsync("Active");
        await Expect(Page.GetByTestId(FeatureTestIds.StripSlug)).ToHaveTextAsync("express-checkout");
    }
}
