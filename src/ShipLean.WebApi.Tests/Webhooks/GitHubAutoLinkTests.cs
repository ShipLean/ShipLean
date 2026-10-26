using System.Text.Json.Nodes;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Webhooks;

namespace ShipLean.WebApi.Tests.Webhooks;

// Only the GitHub secret is set. Pull requests name items by key in the title or the source branch; the webhook links
// them to the Story, Task or Bug with that key, and the close rule then applies as to a hand-made link.
[TestFixture]
public class GitHubAutoLinkTests : WebhookApiTest
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { [WebhookSecrets.GithubKey] = GithubSecret };

    [Test]
    public async Task Opened_WhenTheTitleNamesAStory_ShouldLinkItAsMatchedOnTheTitle()
    {
        var story = await CreateAsync("story", "Named in a title");

        var result = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 40, $"{story.Key} Fix the rate grid", Next()));

        result.Linked.ShouldBe([story.Key]);
        var link = UrlLink(await GetAsync(story.Id));
        link.Url.ShouldBe(WebhookDelivery.GithubUrl(40));
        link.Title.ShouldBe($"{story.Key} Fix the rate grid");
        link.AutoLinked.ShouldBe(new AutoLinkDto(WebhookProvider.Github, AutoLinkMatch.Title));
        link.Card.ShouldNotBeNull().State.ShouldBe(LinkCardState.Open);
        var latest = (await HistoryAsync(story.Id)).Revisions[0];
        latest.Webhook.ShouldBe($"GitHub {WebhookDelivery.GithubRepository}#40");
        latest.Changes.ShouldHaveSingleItem().Field.ShouldBe("links");
    }

    [Test]
    public async Task Opened_WhenTheBranchNamesTicketsInAnyCase_ShouldLinkThemAsMatchedOnTheBranch()
    {
        var task = await CreateAsync("task", "Named in a feature branch");
        var bug = await CreateAsync("bug", "Named in a lower-case branch");

        var feature = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 41, "Rate grid", Next(), branch: $"feature/{task.Key}-rate-grid"));
        var lower = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 42, "A fix", Next(), branch: $"{bug.Key.ToLowerInvariant()}-fix"));

        feature.Linked.ShouldBe([task.Key]);
        lower.Linked.ShouldBe([bug.Key]);
        UrlLink(await GetAsync(bug.Id)).AutoLinked.ShouldBe(new AutoLinkDto(WebhookProvider.Github, AutoLinkMatch.Branch));
    }

    [Test]
    public async Task Opened_WhenTheTitleKeyIsLowerCase_ShouldNotLink()
    {
        var story = await CreateAsync("story", "Lower case in the title");

        var result = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 43, $"{story.Key.ToLowerInvariant()} in prose", Next()));

        result.Linked.ShouldBeEmpty();
        (await GetAsync(story.Id)).Links.ShouldBeEmpty();
    }

    [Test]
    public async Task Opened_WhenKeysNameAFeatureOrNoTicket_ShouldNotLink()
    {
        var feature = await CreateAsync("feature", "Features are never auto-linked");

        var result = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 44, $"{feature.Key} SHIP-99999 UTF-8 SHA-256", Next()));

        result.Linked.ShouldBeEmpty();
        (await GetAsync(feature.Id)).Links.ShouldBeEmpty();
    }

    [Test]
    public async Task Edited_WhenRepeatedOrTheKeyIsRemoved_ShouldKeepOneLinkAndStillCloseOnMerge()
    {
        var story = await CreateAsync("story", "Linked once");
        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 45, $"{story.Key} First", Next()));

        var repeated = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "edited", 45, $"{story.Key} First", Next()));
        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "edited", 45, "No key any more", Next()));
        var afterEdit = await GetAsync(story.Id);
        var merged = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", 45, "No key any more", Next(), state: "closed", merged: true));

        repeated.Linked.ShouldBeEmpty();
        UrlLink(afterEdit).Url.ShouldBe(WebhookDelivery.GithubUrl(45));
        merged.Closed.ShouldBe([story.Key]);
        CategoryOf(await GetAsync(story.Id)).ShouldBe(StateCategory.Completed);
    }

    [Test]
    public async Task Edited_WhenTheTitleGainsAKey_ShouldLinkThen()
    {
        var story = await CreateAsync("story", "Named later");
        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 46, "No key yet", Next()));

        var edited = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "edited", 46, $"{story.Key} Now with a key", Next()));

        edited.Linked.ShouldBe([story.Key]);
    }

    [Test]
    public async Task Edited_WhenSomeoneArchivedTheAutoLink_ShouldNotAddItAgain()
    {
        var story = await CreateAsync("story", "Link removed by hand");
        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 47, $"{story.Key} Reference only", Next()));
        var item = await GetAsync(story.Id);
        using (var removed = await Api.PatchJsonAsync($"api/work-items/{story.Id}", new JsonObject
        {
            ["expectedRevision"] = item.Revision,
            ["changes"] = new JsonObject { ["removeLinkIds"] = new JsonArray(UrlLink(item).Id) },
        }))
        {
            removed.IsSuccessStatusCode.ShouldBeTrue(await removed.Content.ReadAsStringAsync());
        }

        var edited = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "edited", 47, $"{story.Key} Reference only, edited", Next()));

        edited.Linked.ShouldBeEmpty();
        (await GetAsync(story.Id)).Links.ShouldBeEmpty();
    }
}
