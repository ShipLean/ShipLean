using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.Navigation;

// A project made from the picker opens on its first-run backlog; adding the first story ends the first-run state.
[TestFixture]
public class NewProjectFirstRunJourneyTests : SeededAcceptanceTest
{
    private const string ProjectKey = "FRN";

    [Test]
    public async Task J8_NewProject_ShowsEmptyBacklogState()
    {
        var picker = new PickerPicks(Page);
        await picker.Frame.OpenAsync(Links.Backlog("SHIP"));

        // 1. New project, from the picker; Create Sprint 1 stays on.
        await picker.Frame.OpenPickerAsync();
        await Page.GetByTestId(ShellTestIds.PickerNewProject).ClickAsync();
        var form = new NewProjectPage(Page);
        await Expect(form.Form).ToBeVisibleAsync();
        await form.Name.FillAsync("First Run");
        await form.Key.FillAsync(ProjectKey);
        await Expect(form.KeyFree).ToHaveAttributeAsync("data-ok", "true");
        await Expect(Page.GetByTestId(SettingsTestIds.NewProjectSprintToggle)).ToBeCheckedAsync();
        var created = await form.CreateAsync();
        created.Ok.ShouldBeTrue(await created.TextAsync());
        await Expect(Page).ToHaveURLAsync(PickerPicks.EndsWith(Links.Backlog(ProjectKey)));

        // 2. The first-run state: add the first story or import. Sprint 1 exists, so it does not say there are none.
        var backlog = new StoriesBacklog(Page);
        await Expect(backlog.Empty).ToBeVisibleAsync();
        await Expect(backlog.EmptyCreate).ToBeVisibleAsync();
        await Expect(backlog.EmptyImport).ToBeVisibleAsync();
        await Expect(backlog.Empty).ToContainTextAsync($"{ProjectKey}-1");
        await Expect(backlog.Empty).Not.ToContainTextAsync("No sprints yet");
        await Expect(Page.GetByTestId(BacklogTestIds.EmptySprint)).ToHaveCountAsync(0);

        // 3. Add the first story from that state.
        await backlog.EmptyCreate.ClickAsync();
        await Expect(backlog.AddRow).ToBeVisibleAsync();
        var add = await backlog.AddAsync("Sign up");
        add.Ok.ShouldBeTrue(await add.TextAsync());
        await Expect(backlog.Empty).ToHaveCountAsync(0);
        await Expect(backlog.Rows).ToHaveCountAsync(1);
        await Expect(backlog.RowKeys).ToHaveTextAsync([$"{ProjectKey}-1"]);

        // 4. The picker lists the new project, first.
        await picker.Frame.OpenPickerAsync();
        await Expect(picker.Option(ProjectKey)).ToHaveCountAsync(1);
        await Expect(picker.Frame.PickerOptions.First).ToHaveTextAsync(new Regex($"^{ProjectKey}"));
    }
}
