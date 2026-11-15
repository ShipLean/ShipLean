using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Journeys;

// The shared retro note and the New action item dialog in the touch layout (SHIP-727 D5, D6, D8), on the sprint Retro
// tab at 768×1024 (a tablet: touch, but wide enough that the rail doesn't cover the board before L727b). The touch rules
// are checked on the note and the dialog only; the rest of the screen is other tasks'.
[TestFixture]
public class RetroTouchNoteTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private static readonly Guid ProjectId = SeedIds.Projects.Ship;
    private static readonly Guid RetroSprintId = SeedIds.Sprints.Sprint19;

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task RetroNote_WhenMarkedNotNeededOnTouch_ShouldSaveTheReason()
    {
        var text = $"Review waited two days {Guid.NewGuid():N}";
        var pointId = await ArrangeWentBadlyPointAsync(text);
        var note = await OpenNoteAsync(text);

        (await TouchRules.ViolationsAsync(Page, note.Note.Root)).ShouldBeEmpty();
        await note.Note.NotNeeded.TapAsync();
        await Expect(note.Note.WhyText).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, note.Note.Root)).ShouldBeEmpty();

        const string reason = "A one-off: the reviewer was on leave.";
        await note.Note.WhyText.FillAsync(reason);
        var saved = await Page.RunAndWaitForApiAsync($"/api/retros/points/{pointId}/not-needed", "POST", () => note.Note.WhySave.TapAsync());
        saved.Ok.ShouldBeTrue(await saved.TextAsync());
        await Expect(note.Note.None).ToContainTextAsync(reason);
        await Expect(note.Note.NotNeeded).ToHaveCountAsync(0);
        (await TouchRules.ViolationsAsync(Page, note.Note.Root)).ShouldBeEmpty();
    }

    [Test]
    public async Task RetroActionDialog_WhenOpenedFromNoteOnTouch_ShouldFillScreenAndCreateFromTopRight()
    {
        var text = $"Estimates ignore review latency {Guid.NewGuid():N}";
        await ArrangeWentBadlyPointAsync(text);
        var note = await OpenNoteAsync(text);
        var dialog = new RetroActionDialogPart(Page);

        await note.Note.AddAction.TapAsync();
        await Expect(dialog.Title).ToHaveValueAsync(text[..Math.Min(80, text.Length)]);
        await Expect(dialog.Title).Not.ToBeFocusedAsync();

        // Full screen: ✕ top left, Create top right, New | Add to existing under them, the note at the end, no footer Save.
        var frame = await Box(dialog.Dialog);
        frame.X.ShouldBe(0);
        frame.Y.ShouldBe(0);
        frame.Width.ShouldBe(768);
        frame.Height.ShouldBe(1024);
        var close = await Box(note.DialogClose);
        var create = await Box(note.Create);
        close.X.ShouldBeLessThan(16);
        (create.X + create.Width).ShouldBeGreaterThan(768 - 24);
        create.Y.ShouldBeLessThan(close.Y + close.Height);
        await Expect(note.DialogPrimary.GetByTestId(RetroSharedTestIds.RetroActionCreate)).ToHaveTextAsync("Create");
        (await Box(dialog.ModeNew)).Y.ShouldBeGreaterThan(create.Y + create.Height);
        (await Box(note.StartsNote)).Y.ShouldBeGreaterThan((await Box(dialog.Pick.Last)).Y);
        await Expect(dialog.Save).ToHaveCountAsync(0);
        (await TouchRules.ViolationsAsync(Page, dialog.Dialog)).ShouldBeEmpty();

        var created = await Page.RunAndWaitForApiAsync($"/api/projects/{ProjectId}/retros/actions", "POST", () => note.Create.TapAsync());
        created.Ok.ShouldBeTrue(await created.TextAsync());
        await Expect(dialog.Dialog).ToHaveCountAsync(0);
        await Expect(note.Note.Pin).ToBeVisibleAsync();
        await Expect(note.Note.AddAction).ToHaveCountAsync(0);
    }

    private async Task<RetroTouchNotePart> OpenNoteAsync(string text)
    {
        await Page.SetViewportSizeAsync(768, 1024);
        await Page.GotoAsync(Links.SprintRetro(Project, RetroSprintId, ("cat", "wentBadly")));
        var retro = new SprintRetroPage(Page);
        await Expect(retro.PointOf(text)).ToBeVisibleAsync();
        return new RetroTouchNotePart(Page, retro.PointOf(text));
    }

    private static async Task<LocatorBoundingBoxResult> Box(ILocator locator)
    {
        var box = await locator.BoundingBoxAsync();
        box.ShouldNotBeNull();
        return box;
    }

    private async Task<Guid> ArrangeWentBadlyPointAsync(string text)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var seed = new RetroSeed(connection, ProjectId);
        var retroId = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, SprintId: RetroSprintId, AgentName: $"agent-{Guid.NewGuid():N}"));
        return await seed.PointAsync(retroId, new RetroSeedPoint(RetroCategory.WentBadly, text));
    }
}
