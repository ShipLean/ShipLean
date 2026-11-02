using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.PlanningAids;

// Search and collapsible sections in Planning on the seeded Storefront project (SHIP), signed in as Joel. The sections'
// state is the signed-in user's saved view:backlog preference, so the tests run in order on one database:
// Order(1) searches and moves one row, Order(2) toggles the running sprint, Order(3) collapses in two projects.
[TestFixture]
public class SearchTests : SeededAcceptanceTest
{
    private const string ViewPreference = "/api/me/preferences/view%3Abacklog";

    [Test]
    [Order(1)]
    public async Task Search_WhenOn_ShouldStopDraggingAndKeepMoves()
    {
        var planning = new PlanningBacklog(Page);
        var keys = new BacklogKeys(Page);
        var saves = new List<string>();
        Page.Request += (_, request) =>
        {
            if (request.Method == "PUT" && new Uri(request.Url).AbsolutePath == ViewPreference)
            {
                saves.Add(request.Url);
            }
        };
        await planning.OpenAsync("SHIP");
        await Expect(keys.Row("SHIP-156")).ToBeVisibleAsync();
        await Expect(keys.Row("SHIP-141")).ToHaveCountAsync(0);

        // A search that hits the running sprint (closed until now) opens it.
        await keys.SearchBox.FillAsync("SHIP-141");
        await Expect(keys.Row("SHIP-141")).ToBeVisibleAsync();
        await Expect(planning.Section("Sprint 24").GetByTestId(BacklogTestIds.SectionToggle)).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(planning.Section("Sprint 24").GetByTestId(BacklogTestIds.SectionToggle)).ToBeDisabledAsync();
        await Expect(Page.GetByTestId(BacklogTestIds.Row)).ToHaveCountAsync(1);

        // Dragging and the pull line are off, and the page says so.
        await Expect(Page.GetByTestId(BacklogTestIds.DragOffBanner)).ToContainTextAsync("Moving to a sprint, the top or the bottom still works");
        await Expect(keys.Row("SHIP-141").GetByTestId(BacklogTestIds.Grip)).ToBeDisabledAsync();
        await Expect(Page.GetByTestId(BacklogTestIds.PullLine)).ToHaveCountAsync(0);

        // Moves by key still work: X and M then a sprint.
        await keys.TabToListAsync();
        await keys.PressAsync("x");
        await keys.PressAsync("m");
        var moved = await Page.RunAndWaitForApiAsync(
            $"/api/projects/{SeedIds.Projects.Ship}/backlog/bulk",
            "POST",
            () => keys.PressAsync("2"));
        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        var result = (await moved.JsonAsync()).GetValueOrDefault();
        result.GetProperty("written").GetArrayLength().ShouldBe(1);
        await Expect(keys.Row("SHIP-141")).ToHaveAttributeAsync("data-section", SeedIds.Sprints.Sprint25.ToString());

        // Clearing the search puts the sections back as they were saved: the running sprint closed again. None of it was saved.
        await keys.SearchBox.FocusAsync();
        await keys.PressAsync("Escape");
        await Expect(keys.Row("SHIP-129")).ToHaveCountAsync(0);
        await Expect(keys.Row("SHIP-141")).ToBeVisibleAsync();
        await Expect(planning.Section("Sprint 24").GetByTestId(BacklogTestIds.SectionToggle)).ToHaveAttributeAsync("aria-expanded", "false");
        saves.ShouldBeEmpty();
    }

    [Test]
    [Order(2)]
    public async Task Sections_WhenFirstOpened_ShouldStartRunningSprintClosedAndKeepToggles()
    {
        var planning = new PlanningBacklog(Page);
        var keys = new BacklogKeys(Page);
        await planning.OpenAsync("SHIP");
        await Expect(keys.Row("SHIP-156")).ToBeVisibleAsync();
        var running = planning.Section("Sprint 24").GetByTestId(BacklogTestIds.SectionToggle);

        await Expect(running).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(planning.Section("Sprint 25").GetByTestId(BacklogTestIds.SectionToggle)).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(planning.Section("Backlog").GetByTestId(BacklogTestIds.SectionToggle)).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(keys.Row("SHIP-129")).ToHaveCountAsync(0);

        var saved = await Page.RunAndWaitForApiAsync(ViewPreference, "PUT", () => running.ClickAsync());
        saved.Ok.ShouldBeTrue(saved.Status.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Expect(keys.Row("SHIP-129")).ToBeVisibleAsync();

        await Page.ReloadAsync();
        await Expect(keys.Row("SHIP-156")).ToBeVisibleAsync();
        await Expect(keys.Row("SHIP-129")).ToBeVisibleAsync();
    }

    [Test]
    [Order(3)]
    public async Task Sections_WhenCollapsedInTwoProjects_ShouldKeepEachProjectsOwn()
    {
        var planning = new PlanningBacklog(Page);
        var keys = new BacklogKeys(Page);
        await planning.OpenAsync("SHIP");
        await Expect(keys.Row("SHIP-151")).ToBeVisibleAsync();
        var saved = await Page.RunAndWaitForApiAsync(ViewPreference, "PUT", () => planning.Section("Sprint 25").GetByTestId(BacklogTestIds.SectionToggle).ClickAsync());
        saved.Ok.ShouldBeTrue(saved.Status.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Expect(keys.Row("SHIP-151")).ToHaveCountAsync(0);

        // A second project gets a sprint and collapses it; SHIP's entries stay in the saved preference.
        await planning.OpenAsync("MOB");
        var created = await Page.RunAndWaitForApiAsync(
            $"/api/projects/{SeedIds.Projects.Mob}/sprints",
            "POST",
            () => Page.GetByTestId(BacklogTestIds.PlanningCreateSprint).ClickAsync());
        created.Ok.ShouldBeTrue(await created.TextAsync());
        var mobSprint = (await created.JsonAsync()).GetValueOrDefault();
        var mobName = mobSprint.GetProperty("name").GetString()!;
        var mobSprintId = mobSprint.GetProperty("id").GetGuid().ToString();
        var toggle = planning.Section(mobName).GetByTestId(BacklogTestIds.SectionToggle);
        await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "true");
        var mobSaved = await Page.RunAndWaitForApiAsync(ViewPreference, "PUT", () => toggle.ClickAsync());
        mobSaved.Ok.ShouldBeTrue(mobSaved.Status.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Expect(toggle).ToHaveAttributeAsync("aria-expanded", "false");

        var entries = await CollapsedEntriesAsync();
        entries.ShouldContain($"{SeedIds.Projects.Mob}:{mobSprintId}");
        entries.ShouldContain($"{SeedIds.Projects.Ship}:{SeedIds.Sprints.Sprint25}");
        // Order(2) opened the running sprint, so it is not a collapsed entry any more.
        entries.ShouldNotContain($"{SeedIds.Projects.Ship}:{SeedIds.Sprints.Sprint24}");

        // Back in SHIP, Sprint 25 is still closed and the Backlog is open; MOB's own closed sprint stays closed after a reload.
        await planning.OpenAsync("SHIP");
        await Expect(planning.Section("Sprint 25").GetByTestId(BacklogTestIds.SectionToggle)).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(planning.Section("Backlog").GetByTestId(BacklogTestIds.SectionToggle)).ToHaveAttributeAsync("aria-expanded", "true");
        await planning.OpenAsync("MOB");
        await Expect(planning.Section(mobName).GetByTestId(BacklogTestIds.SectionToggle)).ToHaveAttributeAsync("aria-expanded", "false");
    }

    private async Task<List<string>> CollapsedEntriesAsync()
    {
        var preference = await PlanningAidsApi.ReadAsync(Page.Context.APIRequest, "/api/me/preferences/view%3Abacklog");
        return [.. preference.GetProperty("json").GetProperty("collapsedSections").EnumerateArray().Select(e => e.GetString()!)];
    }
}
