using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.Application.Features.Webhooks;
using ShipLean.TestSupport.TestIds;
using ShipLean.TestSupport.Webhooks;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Webhooks;

// The GitHub webhook is on. Recorded pull_request payloads are posted to the running host, as GitHub would: a pull
// request whose branch names SHIP-147 links to it with a card, and its merge closes the story. Its own class: the item
// is closed.
[TestFixture]
public class WorkItemWebhookTests : SeededAcceptanceTest
{
    private const string Secret = "acceptance-github-secret";

    private static readonly DateTimeOffset Opened = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { [WebhookSecrets.GithubKey] = Secret };

    [Test]
    public async Task WorkItem_WhenAPullRequestNamingItOpensThenMerges_ShouldShowTheLinkThenCloseTheItem()
    {
        var screen = new WorkItemScreen(Page);
        var links = new LinksPanel(Page);
        var side = new SideRailPanel(Page);
        using var github = Host.CreateClient();

        using (var opened = await github.SendAsync(WebhookDelivery.Github(Secret, "opened", 418, "Export the sprint report", Opened, branch: "ship-147-csv-export").ToRequest()))
        {
            opened.IsSuccessStatusCode.ShouldBeTrue(await opened.Content.ReadAsStringAsync());
        }
        await screen.OpenAsync("SHIP", "SHIP-147");

        await Expect(links.Rows.GetByTestId(WorkItemTestIds.LinkCard)).ToHaveTextAsync("Open");
        await Expect(links.Rows.GetByTestId(WorkItemTestIds.LinkAutoLinked)).ToHaveTextAsync("auto");
        await Expect(Page.GetByTestId(WorkItemTestIds.StateButton)).ToContainTextAsync("New");

        using (var merged = await github.SendAsync(WebhookDelivery.Github(
            Secret, "closed", 418, "Export the sprint report", Opened.AddHours(2), branch: "ship-147-csv-export", state: "closed", merged: true).ToRequest()))
        {
            merged.IsSuccessStatusCode.ShouldBeTrue(await merged.Content.ReadAsStringAsync());
        }
        await Page.ReloadAsync();
        await screen.Screen.WaitForAsync();

        await Expect(Page.GetByTestId(WorkItemTestIds.StateButton)).ToContainTextAsync("Closed");
        await Expect(links.Rows.GetByTestId(WorkItemTestIds.LinkCard)).ToHaveTextAsync("Merged");
        await Expect(side.Revisions.First.GetByTestId(WorkItemTestIds.RevisionWho)).ToHaveTextAsync($"GitHub {WebhookDelivery.GithubRepository}#418");
        await Expect(side.Revisions.First.GetByTestId(WorkItemTestIds.RevisionWebhook)).ToHaveTextAsync("webhook");
    }
}
