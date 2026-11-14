using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.List;

// All sprints on a phone (SHIP-723) on the seeded Storefront project (Sprints 19 to 26), signed in as Joel. The
// create test adds a Sprint 27, so it checks only that one.
[TestFixture]
public class SprintsListPhoneTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Sprints_WhenOpenedOnPhone_ShouldListEverySprintAsRowWithoutSidewaysScroll()
    {
        var sprints = new AllSprintsPhone(Page);
        await sprints.OpenAsync("SHIP");

        // The create test may have added a Sprint 27 to the class's database first, so only the seeded ones are named.
        for (var number = 19; number <= 26; number++)
        {
            await Expect(sprints.RowOf($"Sprint {number}")).ToHaveCountAsync(1);
        }

        await Expect(sprints.Rows.First).ToContainTextAsync("Sprint 24");
        await Expect(sprints.Velocity).ToBeVisibleAsync();
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).ShouldBeTrue();
        foreach (var region in new[] { sprints.Toolbar, sprints.List, sprints.Velocity })
        {
            (await TouchRules.ViolationsAsync(Page, region)).ShouldBeEmpty();
        }
    }

    [Test]
    public async Task Sprints_WhenNewSprintCreatedOnPhone_ShouldAddItToTheList()
    {
        var sprints = new AllSprintsPhone(Page);
        await sprints.OpenAsync("SHIP");

        await sprints.NewSprintButton.TapAsync();

        await Expect(sprints.Dialog).ToBeVisibleAsync();
        await Expect(sprints.Name).ToHaveValueAsync("Sprint 27");
        var box = await sprints.Dialog.BoundingBoxAsync();
        var viewport = Page.ViewportSize!;
        box!.Width.ShouldBe(viewport.Width);
        box.Height.ShouldBe(viewport.Height);
        (await TouchRules.ViolationsAsync(Page, sprints.Dialog)).ShouldBeEmpty();

        var created = await sprints.CreateAsync(SeedIds.Projects.Ship.ToString());

        created.Ok.ShouldBeTrue();
        await Expect(sprints.Dialog).ToHaveCountAsync(0);
        await Expect(sprints.RowOf("Sprint 27")).ToBeVisibleAsync();
    }
}
