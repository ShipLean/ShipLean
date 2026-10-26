using System.Text.Json.Nodes;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.BoardSettings;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.BoardSettings.Styles;

// A teammate saves another section between this user's open and Save. Its own class: it changes the seeded board.
[TestFixture]
public class StylesPaneMergeTests : SeededAcceptanceTest
{
    [Test]
    public async Task BoardSettings_WhenTeammateSavedOtherSections_ShouldKeepTheirsApplyMine()
    {
        var pane = new StylesPane(Page);
        await pane.OpenAsync("SHIP");
        await Expect(pane.Rows).ToHaveCountAsync(2);
        await pane.Names.Nth(1).FillAsync("Spike work");

        await using var tom = await BoardSettingsTeammate.SignInAsTomAsync(Page, BaseUrl);
        await tom.SaveSectionAsync(SeedIds.Boards.ShipStories, "annotations", new JsonObject
        {
            ["tasks"] = false,
            ["stories"] = false,
            ["bugs"] = false,
            ["features"] = false,
        });
        await pane.SaveAndWaitAsync();

        // The open dialog is not rebased live (nothing refetches its settings), so the merge shows at Save, in a toast.
        await Expect(Page.GetByTestId(ComponentTestIds.ToastHost)).ToContainTextAsync("Someone else changed");

        var saved = await tom.ReadSettingsAsync(SeedIds.Boards.ShipStories);
        saved["styleRules"]![1]!["name"]!.GetValue<string>().ShouldBe("Spike work");
        saved["annotations"]!["tasks"]!.GetValue<bool>().ShouldBeFalse();

        // The merged result is what this user sees when the settings are opened again.
        await Page.GotoAsync(Links.BoardSettings("SHIP", "stories", "annotations"));
        await Expect(Page.GetByTestId(BoardSettingsTestIds.AnnotationTasks)).Not.ToBeCheckedAsync();
    }
}
