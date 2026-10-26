using System.Net;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Webhooks;

namespace ShipLean.WebApi.Tests.Webhooks;

// Only the GitLab secret is set. Merge Request Hook deliveries with the token: the same close rule and auto-linking as
// GitHub, with GitLab's "/-/" paths.
[TestFixture]
public class GitLabWebhookTests : WebhookApiTest
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { [WebhookSecrets.GitlabKey] = GitlabSecret };

    [Test]
    public async Task GitHub_WhenOnlyTheGitlabSecretIsSet_ShouldBeNotFound()
    {
        using var response = await PostAsync(WebhookDelivery.GithubPing(GithubSecret));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestCase("wrong-token")]
    [TestCase(null)]
    public async Task Delivery_WhenTheTokenIsWrongOrMissing_ShouldBeUnauthorized(string? token)
    {
        using var response = await PostAsync(WebhookDelivery.Gitlab(GitlabSecret, "open", 1, "Title", Next()).WithHeader("X-Gitlab-Token", token));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Unauthorized)).Code.ShouldBe(ApiErrorCodes.Unauthorized);
    }

    [Test]
    public async Task Merge_WhenAnItemLinksTheMergeRequestWithoutTheDashPath_ShouldCloseItAndNameIt()
    {
        var story = await CreateAsync("story", "Ships with MR 7");
        await LinkAsync(story.Id, $"https://gitlab.example.com/{WebhookDelivery.GitlabRepository}/merge_requests/7");

        var result = await DeliverAsync(WebhookDelivery.Gitlab(GitlabSecret, "merge", 7, "Fix the rate grid", Next(), state: "merged"));

        result.Closed.ShouldBe([story.Key]);
        var item = await GetAsync(story.Id);
        CategoryOf(item).ShouldBe(StateCategory.Completed);
        UrlLink(item).Card.ShouldNotBeNull().Source.ShouldBe(WebhookProvider.Gitlab);
        (await HistoryAsync(story.Id)).Revisions[0].Webhook.ShouldBe($"GitLab {WebhookDelivery.GitlabRepository}!7");
    }

    [Test]
    public async Task Close_WhenNotMerged_ShouldUpdateOnlyTheCard()
    {
        var story = await CreateAsync("story", "MR 8 is abandoned");
        await LinkAsync(story.Id, WebhookDelivery.GitlabUrl(8));

        var result = await DeliverAsync(WebhookDelivery.Gitlab(GitlabSecret, "close", 8, "Abandoned", Next(), state: "closed"));

        result.Closed.ShouldBeEmpty();
        var item = await GetAsync(story.Id);
        CategoryOf(item).ShouldBe(StateCategory.Proposed);
        UrlLink(item).Card.ShouldNotBeNull().State.ShouldBe(LinkCardState.Closed);
    }

    [Test]
    public async Task Open_WhenTheBranchNamesATask_ShouldLinkItThenCloseItOnMerge()
    {
        var task = await CreateAsync("task", "Named in a GitLab branch");
        var branch = $"{task.Key.ToLowerInvariant()}-rate-grid";

        var opened = await DeliverAsync(WebhookDelivery.Gitlab(GitlabSecret, "open", 9, "Rate grid", Next(), branch: branch));
        var merged = await DeliverAsync(WebhookDelivery.Gitlab(GitlabSecret, "merge", 9, "Rate grid", Next(), branch: branch, state: "merged"));

        opened.Linked.ShouldBe([task.Key]);
        merged.Closed.ShouldBe([task.Key]);
        UrlLink(await GetAsync(task.Id)).AutoLinked.ShouldBe(new AutoLinkDto(WebhookProvider.Gitlab, AutoLinkMatch.Branch));
    }
}
