using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Touch;

// The Stories tree and the Features tree on a phone (SHIP-716 D6), on the seeded Storefront project (SHIP). The touch rules are
// checked on the tree's own rows and headers, never the whole screen.
[TestFixture]
public class TreeTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    // A group's header, by the last name in its path.
    private ILocator Group(string title) =>
        Page.GetByTestId(BacklogTestIds.TreeGroup).Filter(new() { Has = Page.GetByTestId(BacklogTestIds.TreeGroupPath).Filter(new() { HasTextRegex = new($"(^| › ){title}$") }) });

    private ILocator Row(string key) =>
        Page.GetByTestId(BacklogTestIds.Row).Filter(new() { Has = Page.GetByTestId(BacklogTestIds.Key).Filter(new() { HasTextRegex = new($"^{key}$") }) });

    // The keys on screen in order, group headers and rows alike (the No parent group has no key).
    private async Task<List<string>> KeysAsync() =>
        [.. await Page.GetByTestId(BacklogTestIds.TreeGroup).Or(Page.GetByTestId(BacklogTestIds.Row)).EvaluateAllAsync<string[]>(
            "els => els.map(e => e.getAttribute('data-key') ?? '')")];

    // The keys of the rows shown under a group, up to the next header.
    private async Task<List<string>> UnderAsync(string groupKey)
    {
        var keys = await KeysAsync();
        var groups = await Page.GetByTestId(BacklogTestIds.TreeGroup).EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('data-key') ?? '')");
        return keys.SkipWhile(k => k != groupKey).Skip(1).TakeWhile(k => !groups.Contains(k)).ToList();
    }

    [Test]
    public async Task Row_WhenDroppedOnFeatureHeaderOnPhone_ShouldReparentAndRegroup()
    {
        await Page.GotoAsync(Links.Backlog("SHIP", mode: "tree"));
        await Group("Mobile sign-in").WaitForAsync();
        (await UnderAsync("SHIP-101")).ShouldContain("SHIP-141");
        var tree = Page.GetByTestId(BacklogTestIds.TouchTree);
        (await TouchRules.ViolationsAsync(Page, tree)).ShouldBeEmpty();

        var patched = Page.RunAndWaitForResponseAsync(
            () => TouchDrag.ByHandleAsync(Page, Row("SHIP-141").GetByTestId(BacklogTestIds.TouchHandle), Group("Mobile sign-in")),
            r => r.Request.Method == "PATCH" && r.Url.EndsWith($"/api/work-items/{SeedIds.WorkItems.Ship141}", StringComparison.Ordinal));

        (await patched).Ok.ShouldBeTrue();
        await Expect(Group("Mobile sign-in")).ToHaveAttributeAsync("data-over", "false");
        (await UnderAsync("SHIP-102")).ShouldContain("SHIP-141");
        (await UnderAsync("SHIP-101")).ShouldNotContain("SHIP-141");

        await Page.ReloadAsync();
        await Group("Mobile sign-in").WaitForAsync();
        (await UnderAsync("SHIP-102")).ShouldContain("SHIP-141");
        (await UnderAsync("SHIP-101")).ShouldNotContain("SHIP-141");
        (await TouchRules.ViolationsAsync(Page, Page.GetByTestId(BacklogTestIds.TouchTree))).ShouldBeEmpty();
    }

    [Test]
    public async Task FeaturesTree_WhenOpenedOnPhone_ShouldShowRowsNotTable()
    {
        await Page.GotoAsync(Links.Backlog("SHIP", "features", "tree"));
        var tree = Page.GetByTestId(BacklogTestIds.TouchTree);
        await tree.WaitForAsync();
        await Expect(Page.GetByTestId(BacklogTestIds.TreeGroup).First).ToBeVisibleAsync();

        var list = Page.GetByTestId(BacklogTestIds.List);
        (await list.EvaluateAsync<int>("el => el.querySelectorAll('table').length")).ShouldBe(0);
        await Expect(Page.GetByTestId(BacklogTestIds.TouchHandle)).ToHaveCountAsync(0);
        (await Page.GetByTestId(BacklogTestIds.Row).CountAsync()).ShouldBeGreaterThan(0);
        (await TouchRules.ViolationsAsync(Page, tree)).ShouldBeEmpty();
    }
}
