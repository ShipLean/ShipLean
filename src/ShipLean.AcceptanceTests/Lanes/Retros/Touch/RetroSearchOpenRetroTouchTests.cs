using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Retros;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Retros.Touch;

// The open retro of Retrospectives › Search on a phone (SHIP-730 D2, D5): the whole retro full screen at the clicked point,
// ‹ › stepping through the results by replacing the address entry. Each test opens by URL, so none depends on the rows.
[TestFixture]
public class RetroSearchOpenRetroTouchTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private ILocator Dialog => Page.GetByTestId(RetrosTestIds.RetrosOpenRetro);

    private ILocator Previous => Page.GetByTestId(RetrosTestIds.RetrosOpenRetroPrevious);

    private ILocator Next => Page.GetByTestId(RetrosTestIds.RetrosOpenRetroNext);

    private ILocator Position => Page.GetByTestId(RetrosTestIds.RetrosOpenRetroPosition);

    private RetroNotePart NoteOf(string text) =>
        new(Dialog.GetByTestId(RetroSharedTestIds.RetroNote).Filter(new() { HasText = text }));

    [Test]
    public async Task OpenRetro_WhenLoadedWithSelectedAndPoint_ShouldShowItFullScreenAtThePoint()
    {
        var arranged = await ArrangeAsync();
        await OpenAsync(arranged.Word, arranged.Retros[0]);

        await Expect(Dialog).ToBeVisibleAsync();
        await Expect(Dialog).ToContainTextAsync(arranged.Retros[0].Title);
        await Expect(Position).ToContainTextAsync("of 2");
        var close = await TouchShell.DialogClose(Dialog).BoundingBoxAsync();
        close.ShouldNotBeNull();
        close.X.ShouldBeLessThan(60);
        close.Y.ShouldBeLessThan(80);
        var note = NoteOf(arranged.Retros[0].Text);
        await Expect(note.Root).ToHaveAttributeAsync("data-highlighted", "true");
        await Expect(note.Root).ToBeInViewportAsync();
        (await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= document.documentElement.clientWidth")).ShouldBeTrue();
        (await TouchRules.ViolationsAsync(Page, Previous)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, Next)).ShouldBeEmpty();
    }

    [Test]
    public async Task OpenRetro_WhenSteppedOnAPhone_ShouldReplaceTheEntryAndDisableAtTheEnds()
    {
        var arranged = await ArrangeAsync();
        await OpenAsync(arranged.Word, arranged.Retros[0]);
        // The results' order is the server's: the one showing "Result 1" is first.
        var firstText = await Position.TextContentAsync() ?? "";
        var first = firstText.Contains("Result 1 of") ? arranged.Retros[0] : arranged.Retros[1];
        var last = first == arranged.Retros[0] ? arranged.Retros[1] : arranged.Retros[0];
        await OpenAsync(arranged.Word, first);
        await Expect(Previous).ToBeDisabledAsync();
        await Expect(Next).ToBeEnabledAsync();

        var before = await Page.EvaluateAsync<int>("history.length");
        await Next.TapAsync();

        await Expect(Dialog).ToContainTextAsync(last.Title);
        await Expect(Position).ToContainTextAsync("Result 2 of 2");
        await Expect(Page).ToHaveURLAsync(new Regex($"point={last.PointId}"));
        (await Page.EvaluateAsync<int>("history.length")).ShouldBe(before);
        await Expect(Next).ToBeDisabledAsync();
        await Expect(Previous).ToBeEnabledAsync();
    }

    [Test]
    public async Task OpenRetro_WhenClosed_ShouldDropSelectedAndPoint()
    {
        var arranged = await ArrangeAsync();
        await OpenAsync(arranged.Word, arranged.Retros[0]);

        await TouchShell.DialogClose(Dialog).TapAsync();

        await Expect(Dialog).ToHaveCountAsync(0);
        Page.Url.ShouldNotContain("selected=");
        Page.Url.ShouldNotContain("point=");
        Page.Url.ShouldContain("q=");
    }

    [Test]
    public async Task OpenRetro_WhenActionItemTapped_ShouldOpenTheSharedDialog()
    {
        var arranged = await ArrangeAsync();
        await OpenAsync(arranged.Word, arranged.Retros[0]);

        await NoteOf(arranged.Retros[0].Text).AddAction.TapAsync();

        await Expect(Page.GetByTestId(RetroSharedTestIds.RetroActionDialog)).ToBeVisibleAsync();
    }

    [TearDown]
    public async Task SettleSeededPointsAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await connection.ExecuteAsync("UPDATE retro_point SET archived_at = now() WHERE archived_at IS NULL AND text LIKE 'Phone open %'");
    }

    private async Task OpenAsync(string word, Seeded retro)
    {
        await Page.GotoAsync(Links.Retros(Project, ("q", word), ("selected", retro.RetroId.ToString()), ("point", retro.PointId.ToString())));
        await Dialog.WaitForAsync();
    }

    private sealed record Seeded(Guid RetroId, Guid PointId, string Title, string Text);

    private sealed record Arranged(string Word, Seeded[] Retros);

    // Two stories, each with a retro whose Improve point holds the same unique word, so a search for it has two results.
    private async Task<Arranged> ArrangeAsync()
    {
        await using var connection = await OpenConnectionAsync();
        var word = "quokka" + Guid.NewGuid().ToString("N")[..8];
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retros = new List<Seeded>();
        for (var i = 1; i <= 2; i++)
        {
            var title = $"Phone open story {i} {word}";
            var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle(title).InsertAsync(connection);
            var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.SubAgent, WorkItemId: story, AgentName: $"agent-{i}"));
            var text = $"Phone open point {i} {word}";
            var point = await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, text));
            retros.Add(new Seeded(retro, point, title, text));
        }

        return new Arranged(word, [.. retros]);
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }
}
