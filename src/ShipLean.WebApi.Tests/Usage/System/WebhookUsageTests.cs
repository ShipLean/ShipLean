using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Usage;
using ShipLean.TestSupport.Webhooks;
using ShipLean.WebApi.Tests.Webhooks;

namespace ShipLean.WebApi.Tests.Usage.System;

// A processed pull or merge request delivery is tracked with its provider, and with the project of the items that link it.
[TestFixture]
public class WebhookUsageTests : WebhookApiTest
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        [WebhookSecrets.GithubKey] = GithubSecret,
        [WebhookSecrets.GitlabKey] = GitlabSecret,
    };

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    [Test]
    public async Task GitHubWebhook_WhenPullRequestLinksItem_ShouldTrackWebhookReceivedWithProviderAndProject()
    {
        var story = await CreateAsync("story", "Webhook usage");

        await DeliverAsync(WebhookDelivery.Github(GithubSecret, "opened", 901, $"{story.Key} Fix", Next()));

        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.WebhookReceived).ShouldHaveSingleItem();
        tracked.Surface.ShouldBe(UsageSurface.System);
        tracked.Actor.ShouldBe(UsageActor.System);
        tracked.Source.ShouldBe("system:PullRequestWebhooks");
        tracked.Properties[UsageProps.Provider].ShouldBe("github");
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
        tracked.ProjectCount.ShouldBe(1);
        tracked.Properties.Count.ShouldBe(1);
    }

    [Test]
    public async Task GitLabWebhook_WhenNoItemMatches_ShouldTrackWithNoProject()
    {
        await DeliverAsync(WebhookDelivery.Gitlab(GitlabSecret, "open", 902, "Nothing names an item", Next()));

        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.WebhookReceived).ShouldHaveSingleItem();
        tracked.Properties[UsageProps.Provider].ShouldBe("gitlab");
        tracked.ProjectId.ShouldBeNull();
        tracked.ProjectCount.ShouldBe(0);
    }

    [Test]
    public async Task GitHubWebhook_WhenPing_ShouldNotTrack()
    {
        using var response = await PostAsync(WebhookDelivery.GithubPing(GithubSecret));

        response.IsSuccessStatusCode.ShouldBeTrue();
        Recorder.Events.ShouldNotContain(e => e.Name == UsageEventNames.WebhookReceived);
    }
}
