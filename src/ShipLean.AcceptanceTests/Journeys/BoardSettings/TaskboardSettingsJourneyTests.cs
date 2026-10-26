using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.BoardSettings;

// Joel changes SHIP's Sprint 24 Taskboard settings, which apply at once; Tom has the same Taskboard open in a page of his own.
[TestFixture]
public class TaskboardSettingsJourneyTests : SeededAcceptanceTest
{
    [Test]
    public async Task J4_TaskboardSettings_ApplyOnBoard()
    {
        var settingsPath = $"/api/boards/{SeedIds.Boards.ShipTaskboard}/settings";
        await using var tom = await SecondViewer.SignInAsTomAsync(Page, BaseUrl);
        var joelsBoard = new SprintTaskboard(Page);
        var tomsBoard = new SprintTaskboard(tom.Page);
        await tomsBoard.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await joelsBoard.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        var dialog = new TaskboardSettingsDialog(Page);
        var styled = new Regex(".+");
        // The seeded Taskboard has a rule for priority 1 (SHIP-172) and none for priority 2 (SHIP-171).
        var picker = joelsBoard.TaskCard("SHIP-172");
        var plain = joelsBoard.TaskCard("SHIP-171");
        await Expect(picker.GetByTestId(TaskboardTestIds.CardPoints)).ToHaveCountAsync(0);
        await Expect(plain).Not.ToHaveAttributeAsync("data-style-rule", styled);

        await dialog.Open.ClickAsync();
        await Expect(dialog.FieldsPane).ToBeVisibleAsync();
        await Expect(Page.GetByTestId(SettingsDialogTestIds.Save)).ToHaveCountAsync(0);

        // A card field switched on shows on the cards behind the dialog as soon as it is saved.
        var saved = await Page.RunAndWaitForApiAsync(settingsPath, "PUT", () => dialog.ShowPoints.CheckAsync());
        saved.Ok.ShouldBeTrue();
        await Expect(picker.GetByTestId(TaskboardTestIds.CardPoints)).ToHaveTextAsync("2");
        await Expect(tomsBoard.TaskCard("SHIP-172").GetByTestId(TaskboardTestIds.CardPoints)).ToHaveTextAsync("2");

        // A new style rule is held until its criteria are valid, then saved without a Save button.
        await dialog.NavItem("styles").ClickAsync();
        await Expect(dialog.StylesPane).ToBeVisibleAsync();
        var rules = await dialog.StyleRows.CountAsync();
        await Page.GetByTestId(TaskboardSettingsTestIds.StylesAdd).ClickAsync();
        await Expect(dialog.StyleRows).ToHaveCountAsync(rules + 1);
        var last = rules;

        await dialog.Names.Nth(last).FillAsync("Urgent");
        await dialog.RemoveClause(last).ClickAsync();

        // Closing with the rule's criteria unfinished asks first; the rule has not been saved.
        await Page.GetByTestId(ComponentTestIds.DialogClose).ClickAsync();
        await Expect(dialog.UnfinishedRule).ToBeVisibleAsync();
        await Page.GetByTestId(TaskboardSettingsTestIds.KeepEditing).ClickAsync();
        await Expect(dialog.UnfinishedRule).ToBeHiddenAsync();

        await dialog.AddClause(last).ClickAsync();
        await dialog.ClauseField(last).SelectOptionAsync(new SelectOptionValue { Label = "Priority" });
        var ruleSaved = await Page.RunAndWaitForApiAsync(
            settingsPath,
            "PUT",
            () => dialog.ClauseValue(last).SelectOptionAsync(new SelectOptionValue { Label = "2" }));
        ruleSaved.Ok.ShouldBeTrue();

        await Expect(plain).ToHaveAttributeAsync("data-style-rule", styled);
        await Expect(picker).ToHaveAttributeAsync("data-style-rule", "rule-priority-1");
        await Expect(tomsBoard.TaskCard("SHIP-171")).ToHaveAttributeAsync("data-style-rule", styled);

        await Page.ReloadAsync();
        await joelsBoard.Grid.WaitForAsync();
        await Expect(joelsBoard.TaskCard("SHIP-172").GetByTestId(TaskboardTestIds.CardPoints)).ToHaveTextAsync("2");
        await Expect(joelsBoard.TaskCard("SHIP-171")).ToHaveAttributeAsync("data-style-rule", styled);
    }
}
