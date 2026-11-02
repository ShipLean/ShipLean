using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Filling;

// Typing new stories where the person is, on the seeded Storefront project (SHIP). Each test reads the order back from
// the server, not from the screen. Tests run in order and each adds rows nobody else reads: titles are its own.
// The seeded stack is Sprint 24 (141, 144, 129), Sprint 25 (151, 153, 147), then the Backlog (156 ... 89).
[TestFixture]
public class AddWhereYouAreTests : SeededAcceptanceTest
{
    [Test]
    [Order(1)]
    public async Task Add_WhenTypedBelowARow_ShouldKeepOrderAndParent()
    {
        var backlog = new StoriesBacklog(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        // SHIP-129 is the last row of Sprint 24 and the next row in the list is in Sprint 25.
        await FillingSteps.FocusRowAsync(backlog, "SHIP-129");
        await Page.Keyboard.PressAsync("n");
        await Expect(backlog.AddRow).ToBeVisibleAsync();

        await backlog.AddParent.ClickAsync();
        await Page.GetByText("SHIP-103", new() { Exact = false }).First.ClickAsync();
        await Expect(backlog.AddParent).ToContainTextAsync("Weekly");

        var created = new List<string>();
        foreach (var title in new[] { "Below one", "Below two", "Below three" })
        {
            var response = await FillingSteps.TypeAsync(Page, backlog, title);
            response.Ok.ShouldBeTrue(await response.TextAsync());
            created.AddRange(await FillingSteps.CreatedKeysAsync(response));
        }

        var stack = await backlog.StackAsync();
        var at = stack.FindIndex(e => e.Key == "SHIP-129");
        stack.Skip(at + 1).Take(3).Select(e => e.Key).ShouldBe(created);
        FillingSteps.Titles(stack.Skip(at + 1).Take(3)).ShouldBe(["Below one", "Below two", "Below three"]);
        stack.Skip(at + 1).Take(3).ShouldAllBe(e => e.ParentKey == "SHIP-103" && e.Sprint == "Sprint 24");
        stack[at + 4].Key.ShouldBe("SHIP-151");
    }

    [Test]
    [Order(2)]
    public async Task Add_WhenBelowTheLastRowOfASprint_ShouldLandBelowItInPlanningAndList()
    {
        var backlog = new StoriesBacklog(Page);
        await Page.GotoAsync(Links.Backlog("SHIP", mode: "planning"));
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        // SHIP-147 is the last row of Sprint 25; the row after it in the stack is the Backlog's SHIP-156.
        await FillingSteps.FocusRowAsync(backlog, "SHIP-147");
        await Page.Keyboard.PressAsync("n");
        await Expect(backlog.AddRow).ToBeVisibleAsync();

        // The second Enter comes before the first answer: the queue keeps the order.
        await backlog.AddTitle.FillAsync("Last of sprint one");
        await backlog.AddTitle.PressAsync("Enter");
        var second = Page.RunAndWaitForApiAsync(FillingSteps.ItemsPath, "POST", async () =>
        {
            await backlog.AddTitle.FillAsync("Last of sprint two");
            await backlog.AddTitle.PressAsync("Enter");
        });
        await second;
        await Expect(backlog.Row("SHIP-147")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Last of sprint two").First).ToBeVisibleAsync();

        var section = FillingSteps.Titles(await backlog.SectionAsync("Sprint 25"));
        section.TakeLast(2).ShouldBe(["Last of sprint one", "Last of sprint two"]);
        var stack = await backlog.StackAsync();
        var at = stack.FindIndex(e => e.Key == "SHIP-147");
        FillingSteps.Titles(stack.Skip(at + 1).Take(2)).ShouldBe(["Last of sprint one", "Last of sprint two"]);
        stack[at + 3].Key.ShouldBe("SHIP-156");
    }

    [Test]
    [Order(3)]
    public async Task Add_WhenBelowAMiddleRowInPlanning_ShouldLandDirectlyBelowIt()
    {
        var backlog = new StoriesBacklog(Page);
        await Page.GotoAsync(Links.Backlog("SHIP", mode: "planning"));
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        await FillingSteps.FocusRowAsync(backlog, "SHIP-151");
        await Page.Keyboard.PressAsync("n");
        await Expect(backlog.AddRow).ToBeVisibleAsync();

        var response = await FillingSteps.TypeAsync(Page, backlog, "Middle of sprint");

        response.Ok.ShouldBeTrue(await response.TextAsync());
        var section = await backlog.SectionAsync("Sprint 25");
        section.Skip(1).First().Title.ShouldBe("Middle of sprint");
        section[0].Key.ShouldBe("SHIP-151");
        section[2].Key.ShouldBe("SHIP-153");
    }

    [Test]
    [Order(4)]
    public async Task Add_WhenInSprint_ShouldLandInThatSprint()
    {
        var backlog = new StoriesBacklog(Page);
        await Page.GotoAsync(Links.Backlog("SHIP", mode: "planning"));
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        // The running sprint starts closed: its rows are not on the page until + Add opens it.
        await Expect(backlog.Row("SHIP-141")).ToHaveCountAsync(0);

        await backlog.AddHere("Sprint 24").ClickAsync();
        await Expect(backlog.AddRow).ToBeVisibleAsync();
        await Expect(backlog.Row("SHIP-141")).ToBeVisibleAsync();

        foreach (var title in new[] { "Sprint first", "Sprint second", "Sprint third" })
        {
            var response = await FillingSteps.TypeAsync(Page, backlog, title);
            response.Ok.ShouldBeTrue(await response.TextAsync());
        }

        var section = await backlog.SectionAsync("Sprint 24");
        FillingSteps.Titles(section.Take(3)).ShouldBe(["Sprint first", "Sprint second", "Sprint third"]);
        section.Skip(3).First().Key.ShouldBe("SHIP-141");
        section.ShouldAllBe(e => e.Sprint == "Sprint 24");
    }
}
