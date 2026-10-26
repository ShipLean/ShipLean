using System.Net;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Webhooks;
using ShipLean.WebApi.Features.Webhooks;

namespace ShipLean.WebApi.Tests.Webhooks;

// Only the GitHub secret is set. Items in SHIP link pull requests by hand; signed pull_request deliveries update the
// link cards and close an item once its pull requests have merged. Each test uses its own pull request numbers.
[TestFixture]
public class GitHubCloseRuleTests : WebhookApiTest
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { [WebhookSecrets.GithubKey] = GithubSecret };

    [Test]
    public async Task GitLab_WhenOnlyTheGithubSecretIsSet_ShouldBeNotFound()
    {
        using var response = await PostAsync(WebhookDelivery.Gitlab(GitlabSecret, "merge", 1, "Title", Next(), state: "merged"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Ping_WhenSigned_ShouldAnswerPong()
    {
        (await DeliverAsync(WebhookDelivery.GithubPing(GithubSecret))).Outcome.ShouldBe(WebhookOutcome.Pong);
    }

    [TestCase("sha256=0000000000000000000000000000000000000000000000000000000000000000")]
    [TestCase("not-a-signature")]
    [TestCase(null)]
    public async Task Delivery_WhenSignatureIsWrongOrMissing_ShouldBeUnauthorizedAndChangeNothing(string? signature)
    {
        var story = await CreateAsync("story", "Unsigned");
        await LinkAsync(story.Id, WebhookDelivery.GithubUrl(10));
        var delivery = WebhookDelivery.Github(GithubSecret, "closed", 10, "Title", Next(), state: "closed", merged: true).WithHeader("X-Hub-Signature-256", signature);

        using var response = await PostAsync(delivery);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
        var item = await GetAsync(story.Id);
        CategoryOf(item).ShouldBe(StateCategory.Proposed);
        UrlLink(item).Card.ShouldBeNull();
    }

    [Test]
    public async Task Delivery_WhenTheBodyIsOverTheCap_ShouldBeTooLarge()
    {
        var title = new string('x', WebhookServices.MaxBodyBytes);

        using var response = await PostAsync(WebhookDelivery.Github(GithubSecret, "opened", 11, title, Next()));

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Test]
    public async Task Delivery_WhenSignedWithAnotherSecret_ShouldBeUnauthorized()
    {
        using var response = await PostAsync(WebhookDelivery.GithubPing("some-other-secret"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Merged_WhenAnItemLinksThePullRequest_ShouldCloseItAndNameThePullRequestInHistory()
    {
        var story = await CreateAsync("story", "Ships with PR 12");

        await LinkAsync(story.Id, WebhookDelivery.GithubUrl(12));
        var result = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", 12, "Fix the rate grid", Next(), state: "closed", merged: true));

        result.Closed.ShouldBe([story.Key]);
        var item = await GetAsync(story.Id);
        CategoryOf(item).ShouldBe(StateCategory.Completed);
        item.State.Name.ShouldBe("Closed");
        var card = UrlLink(item).Card.ShouldNotBeNull();
        card.State.ShouldBe(LinkCardState.Merged);
        card.Title.ShouldBe("Fix the rate grid");
        card.Source.ShouldBe(WebhookProvider.Github);
        var latest = (await HistoryAsync(story.Id)).Revisions[0];
        latest.Origin.ShouldBe(RevisionOrigin.Webhook);
        latest.Webhook.ShouldBe($"GitHub {WebhookDelivery.GithubRepository}#12");
        latest.ChangedBy.ShouldBeNull();
        latest.Changes.Select(c => c.Field).ShouldContain("state");
    }

    [Test]
    public async Task ClosedUnmergedThenReopened_WhenAnItemLinksThePullRequest_ShouldUpdateOnlyTheCard()
    {
        var story = await CreateAsync("story", "PR 13 is abandoned");
        await LinkAsync(story.Id, WebhookDelivery.GithubUrl(13));
        var revision = (await GetAsync(story.Id)).Revision;

        var closed = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", 13, "Try something", Next(), state: "closed"));
        var afterClose = await GetAsync(story.Id);
        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "reopened", 13, "Try something", Next()));
        var afterReopen = await GetAsync(story.Id);

        closed.Closed.ShouldBeEmpty();
        UrlLink(afterClose).Card.ShouldNotBeNull().State.ShouldBe(LinkCardState.Closed);
        UrlLink(afterReopen).Card.ShouldNotBeNull().State.ShouldBe(LinkCardState.Open);
        CategoryOf(afterReopen).ShouldBe(StateCategory.Proposed);
        afterReopen.Revision.ShouldBe(revision);
    }

    [Test]
    public async Task Merged_WhenTheItemHasAnotherOpenPullRequest_ShouldWaitUntilBothAreDone()
    {
        var story = await CreateAsync("story", "Two PRs");
        await LinkAsync(story.Id, WebhookDelivery.GithubUrl(14));
        await LinkAsync(story.Id, WebhookDelivery.GithubUrl(15));
        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 15, "Second half", Next()));

        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", 14, "First half", Next(), state: "closed", merged: true));
        var afterFirst = await GetAsync(story.Id);
        var second = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", 15, "Second half", Next(), state: "closed"));

        CategoryOf(afterFirst).ShouldBe(StateCategory.Proposed);
        second.Closed.ShouldBe([story.Key]);
        CategoryOf(await GetAsync(story.Id)).ShouldBe(StateCategory.Completed);
    }

    [Test]
    public async Task Merged_WhenALinkedPullRequestWasNeverReported_ShouldLeaveTheItemOpen()
    {
        var story = await CreateAsync("story", "One PR unknown");
        await LinkAsync(story.Id, WebhookDelivery.GithubUrl(16));
        await LinkAsync(story.Id, WebhookDelivery.GithubUrl(17));

        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", 16, "Known", Next(), state: "closed", merged: true));

        CategoryOf(await GetAsync(story.Id)).ShouldBe(StateCategory.Proposed);
    }

    [Test]
    public async Task Merged_WhenStoredLinksDifferInSchemeCaseSlashFragmentOrSuffix_ShouldStillMatch()
    {
        var files = await CreateAsync("story", "Links the files tab");
        var slash = await CreateAsync("bug", "Links with a trailing slash");
        await LinkAsync(files.Id, "https://GitHub.com/acme/storefront/pull/18/files#diff-1a2b");
        await LinkAsync(slash.Id, "http://github.com/acme/storefront/pull/18/");

        var result = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", 18, "Both", Next(), state: "closed", merged: true));

        result.Closed.Order().ShouldBe(new[] { files.Key, slash.Key }.Order());
    }

    [Test]
    public async Task Merged_WhenTheItemIsAlreadyClosed_ShouldLeaveItAlone()
    {
        var story = await CreateAsync("story", "Closed once");
        await LinkAsync(story.Id, WebhookDelivery.GithubUrl(19));
        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", 19, "Done", Next(), state: "closed", merged: true));
        var revision = (await GetAsync(story.Id)).Revision;

        var again = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", 19, "Done", Next(), state: "closed", merged: true));

        again.Closed.ShouldBeEmpty();
        (await GetAsync(story.Id)).Revision.ShouldBe(revision);
    }

    [Test]
    public async Task Delivery_WhenTheDeliveryIdWasSeenBefore_ShouldBeANoOp()
    {
        var story = await CreateAsync("story", "Replayed");
        await LinkAsync(story.Id, WebhookDelivery.GithubUrl(20));
        var opened = WebhookDelivery.Github(GithubSecret, "opened", 20, "Opened", Next());
        await DeliverAsync(opened);
        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", 20, "Merged", Next(), state: "closed", merged: true));

        var replay = await DeliverAsync(opened);

        replay.Outcome.ShouldBe(WebhookOutcome.Duplicate);
        UrlLink(await GetAsync(story.Id)).Card.ShouldNotBeNull().State.ShouldBe(LinkCardState.Merged);
    }

    [Test]
    public async Task Delivery_WhenAnOlderEventArrivesLate_ShouldNotTurnTheCardBack()
    {
        var story = await CreateAsync("story", "Out of order");
        await LinkAsync(story.Id, WebhookDelivery.GithubUrl(21));
        var early = Next();
        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", 21, "Merged", Next(), state: "closed", merged: true));

        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "edited", 21, "Older edit", early));

        UrlLink(await GetAsync(story.Id)).Card.ShouldNotBeNull().State.ShouldBe(LinkCardState.Merged);
    }

    [Test]
    public async Task Delivery_WhenNoItemLinksThePullRequest_ShouldBeAcknowledged()
    {
        var result = await DeliverAsync(WebhookDelivery.Github(GithubSecret, "closed", 22, "Nobody links me", Next(), state: "closed", merged: true));

        result.Outcome.ShouldBe(WebhookOutcome.Processed);
        result.Linked.ShouldBeEmpty();
        result.Closed.ShouldBeEmpty();
    }
}
