using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Discussion;

[TestFixture]
public class WorkItemMentionTests : SeededAcceptanceTest
{
    [Test]
    public async Task WorkItem_WhenMentionTyped_ShouldSuggestMembers()
    {
        var screen = new WorkItemScreen(Page);
        var discussion = new DiscussionPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-147");

        await discussion.Input.FillAsync("Can you check, @pri");

        await Expect(discussion.MentionOptions).ToHaveCountAsync(1);
        await Expect(discussion.MentionOptions).ToContainTextAsync("Priya");
        await discussion.MentionOptions.First.ClickAsync();
        await Expect(discussion.Input).ToHaveValueAsync(new System.Text.RegularExpressions.Regex(@"^Can you check, @Priya \S+ $"));
        await Expect(discussion.MentionMenu).ToHaveCountAsync(0);
        await Expect(screen.Save).ToBeDisabledAsync();
    }
}
