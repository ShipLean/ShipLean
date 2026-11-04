using System.Text.Json.Nodes;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Webhooks;

namespace ShipLean.WebApi.Tests.Webhooks;

// Only the GitHub secret is set. Pull requests name items by key in the title, the source branch or a "ShipLean IDs:" line
// that opens the body; the webhook links them to the Story, Task or Bug with that key, or to the Stories and Bugs of a
// Feature the body line names, and the close rule then applies as to a hand-made link.
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
    public async Task Opened_WhenTheBodyLineListsKeys_ShouldLinkThemAsMatchedOnTheBody()
    {
        var story = await CreateAsync("story", "Listed first");
        var bug = await CreateAsync("bug", "Listed second");

        var result = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 60, "A clean title", Next(), body: $"\r\n\r\nShipLean IDs: {story.Key}, {bug.Key}\r\n\r\nWhat it does."));

        result.Linked.ShouldBe(new[] { story.Key, bug.Key }, ignoreOrder: true);
        UrlLink(await GetAsync(story.Id)).AutoLinked.ShouldBe(new AutoLinkDto(WebhookProvider.Github, AutoLinkMatch.Body));
    }

    [Test]
    public async Task Merged_WhenTheBodyLineNamesAFeature_ShouldLinkAndCloseItsStoriesAndBugsButNotTheFeatureOrItsTasks()
    {
        var feature = await CreateAsync("feature", "Named in the title and the body");
        var story = await CreateAsync("story", "Under the Feature", feature.Id);
        var bug = await CreateAsync("bug", "Also under the Feature", feature.Id);
        var task = await CreateAsync("task", "Still open under the story", story.Id);
        var other = await CreateAsync("story", "Under nothing");
        var title = $"{feature.Key}: a clean title";
        var body = $"ShipLean IDs: {feature.Key}\n\nWhat it does.";

        var opened = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 61, title, Next(), body: body));
        var merged = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", 61, title, Next(), state: "closed", merged: true, body: body));

        opened.Linked.ShouldBe(new[] { story.Key, bug.Key }, ignoreOrder: true);
        merged.Closed.ShouldBe(new[] { story.Key, bug.Key }, ignoreOrder: true);
        UrlLink(await GetAsync(bug.Id)).AutoLinked.ShouldBe(new AutoLinkDto(WebhookProvider.Github, AutoLinkMatch.Body));
        CategoryOf(await GetAsync(story.Id)).ShouldBe(StateCategory.Completed);
        var featureAfter = await GetAsync(feature.Id);
        featureAfter.Links.ShouldBeEmpty();
        CategoryOf(featureAfter).ShouldBe(StateCategory.Proposed);
        (await GetAsync(task.Id)).Links.ShouldBeEmpty();
        (await GetAsync(other.Id)).Links.ShouldBeEmpty();
    }

    [Test]
    public async Task Opened_WhenOnlyTheTitleNamesAFeature_ShouldNotLinkItsStories()
    {
        var feature = await CreateAsync("feature", "Named in the title for people to read");
        var story = await CreateAsync("story", "Not part of this pull request", feature.Id);

        var result = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 62, $"{feature.Key} Reference screens", Next()));

        result.Linked.ShouldBeEmpty();
        (await GetAsync(story.Id)).Links.ShouldBeEmpty();
    }

    [Test]
    public async Task Opened_WhenTheBodyLineIsNotFirstOrHasNoLabel_ShouldNotLink()
    {
        var story = await CreateAsync("story", "Only quoted in the body");

        var later = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 63, "Quotes the convention", Next(), body: $"For example:\nShipLean IDs: {story.Key}"));
        var unlabelled = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 64, "Mentions a key", Next(), body: $"Follows on from {story.Key}."));

        later.Linked.ShouldBeEmpty();
        unlabelled.Linked.ShouldBeEmpty();
    }

    [Test]
    public async Task Edited_WhenTheBodyGainsTheLine_ShouldLinkThen()
    {
        var story = await CreateAsync("story", "Named in the body later");
        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 65, "No key yet", Next(), body: "What it does."));

        var edited = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "edited", 65, "No key yet", Next(), body: $"ShipLean IDs: {story.Key}\n\nWhat it does."));

        edited.Linked.ShouldBe([story.Key]);
    }

    [Test]
    public async Task Opened_WhenAFeatureHasMoreThanTheLimit_ShouldLinkTheRestOnTheNextEvent()
    {
        var feature = await CreateAsync("feature", "More stories than one event links");
        for (var i = 0; i < 101; i++)
        {
            await CreateAsync("story", $"Story {i}", feature.Id);
        }
        var body = $"ShipLean IDs: {feature.Key}";

        var opened = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 66, "A big Feature", Next(), body: body));
        var edited = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "edited", 66, "A big Feature", Next(), body: body));

        opened.Linked.Count.ShouldBe(100);
        edited.Linked.Count.ShouldBe(1);
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
