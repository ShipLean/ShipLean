using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.IssueIntake;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Webhooks;
using ShipLean.TestSupport.IssueIntake;
using ShipLean.TestSupport.Webhooks;
using ShipLean.WebApi.Tests.Webhooks;

namespace ShipLean.WebApi.Tests.IssueIntake.Routing;

// C1: GitHub "issues" and "issue_comment" deliveries are parsed and sent to the lane interfaces, here one recording double
// for all three. Each test uses its own issue number, since the host (and the double) lasts the whole fixture.
[TestFixture]
public class IssueRoutingTests : WebhookApiTest
{
    private const string SelfLogin = "shiplean-bot";
    private static readonly IssueRef Issue64 = new(IssueRef.GitHubHost, WebhookDelivery.GithubRepository, 64);

    private readonly RecordingIssueHandlers _handlers = new();

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { [WebhookSecrets.GithubKey] = GithubSecret };

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IGitHubIdentity>(new FixedGitHubIdentity(SelfLogin));
        services.AddSingleton<IIssueIntake>(_handlers);
        services.AddSingleton<IIssueReplies>(_handlers);
        services.AddSingleton<IIssueAcceptance>(_handlers);
    };

    [Test]
    public async Task IssueOpened_WhenDelivered_ShouldRouteToIntake()
    {
        var result = await DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, "opened", 101, title: "Filter by Epic", body: "Only one Epic's stories.", labels: ["bug", "ui"]));

        result.Outcome.ShouldBe(WebhookOutcome.Processed);
        result.Item.ShouldBe("SHIP-1");
        result.Linked.ShouldBeEmpty();
        result.Closed.ShouldBeEmpty();
        var call = CallsFor(101).ShouldHaveSingleItem();
        call.Method.ShouldBe(nameof(IIssueIntake.OpenedAsync));
        var issue = call.Event.ShouldBeOfType<IssueEvent>();
        issue.Action.ShouldBe(IssueAction.Opened);
        issue.Issue.ShouldBe(new IssueRef("github.com", "acme/storefront", 101));
        issue.Url.ShouldBe(WebhookDelivery.GithubIssueUrl(101));
        issue.Title.ShouldBe("Filter by Epic");
        issue.Body.ShouldBe("Only one Epic's stories.");
        issue.AuthorLogin.ShouldBe(WebhookDelivery.IssueAuthor);
        issue.AuthorAssociation.ShouldBe("NONE");
        issue.Labels.ShouldBe(["bug", "ui"]);
        issue.Open.ShouldBeTrue();
        issue.SenderIsBot.ShouldBeFalse();
        issue.Reference.ShouldBe("GitHub acme/storefront#101");
    }

    [TestCase("edited", 102, "open")]
    [TestCase("closed", 103, "closed")]
    [TestCase("reopened", 104, "open")]
    public async Task IssueChanged_WhenDelivered_ShouldRouteToIntakeChanged(string action, int number, string state)
    {
        await DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, action, number, state: state));

        var call = CallsFor(number).ShouldHaveSingleItem();
        call.Method.ShouldBe(nameof(IIssueIntake.ChangedAsync));
        var issue = call.Event.ShouldBeOfType<IssueEvent>();
        issue.Action.ToString().ShouldBe(action, StringCompareShould.IgnoreCase);
        issue.Open.ShouldBe(state == "open");
    }

    [TestCase("labeled", 105)]
    [TestCase("assigned", 106)]
    public async Task IssueAction_WhenNotHandled_ShouldBeIgnored(string action, int number)
    {
        var result = await DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, action, number));

        result.Outcome.ShouldBe(WebhookOutcome.Ignored);
        CallsFor(number).ShouldBeEmpty();
    }

    [Test]
    public async Task IssueOpened_WhenBodyIsOver64Kb_ShouldPassItCapped()
    {
        await DeliverAsync(WebhookDelivery.GithubIssue(GithubSecret, "opened", 107, body: new string('x', IssueEvent.MaxTextLength + 1)));

        var issue = CallsFor(107).ShouldHaveSingleItem().Event.ShouldBeOfType<IssueEvent>();
        issue.Body.Length.ShouldBe(IssueEvent.MaxTextLength);
        issue.Body.ShouldEndWith(IssueCommentClassifier.TruncatedNote);
    }

    [Test]
    public async Task IssueComment_OnPullRequest_ShouldBeIgnored()
    {
        var result = await DeliverAsync(WebhookDelivery.GithubIssueComment(GithubSecret, "LGTM", association: "OWNER", number: 120, onPullRequest: true));

        result.Outcome.ShouldBe(WebhookOutcome.Ignored);
        result.Item.ShouldBeNull();
        CallsFor(120).ShouldBeEmpty();
    }

    [TestCase(121, "LGTM", WebhookDelivery.IssueAuthor, "NONE", nameof(IIssueAcceptance.AcceptAsync))]
    [TestCase(122, "/shiplean", WebhookDelivery.Maintainer, "MEMBER", nameof(IIssueIntake.TakeAsync))]
    [TestCase(123, "Could it filter by Feature too?", WebhookDelivery.IssueAuthor, "NONE", nameof(IIssueReplies.ReplyAsync))]
    [TestCase(124, "@maya-maintainer says LGTM", "passer-by", "NONE", nameof(IIssueReplies.MirrorAsync))]
    public async Task IssueComment_WhenDelivered_ShouldRouteByKind(int number, string body, string author, string association, string method)
    {
        var result = await DeliverAsync(WebhookDelivery.GithubIssueComment(GithubSecret, body, author, association, number, commentId: 2470000000 + number));

        result.Outcome.ShouldBe(WebhookOutcome.Processed);
        result.Item.ShouldBe("SHIP-1");
        var call = CallsFor(number).ShouldHaveSingleItem();
        call.Method.ShouldBe(method);
        var comment = call.Event.ShouldBeOfType<IssueCommentEvent>();
        comment.Issue.ShouldBe(Issue64 with { Number = number });
        comment.CommentId.ShouldBe(2470000000 + number);
        comment.CommentUrl.ShouldBe($"{WebhookDelivery.GithubIssueUrl(number)}#issuecomment-{2470000000 + number}");
        comment.Body.ShouldBe(body);
        comment.AuthorLogin.ShouldBe(author);
        comment.AuthorAssociation.ShouldBe(association);
        comment.IssueAuthorLogin.ShouldBe(WebhookDelivery.IssueAuthor);
        comment.AgentName.ShouldBe($"GitHub @{author}");
    }

    [TestCase(130, SelfLogin, "User", "created")]
    [TestCase(131, "SHIPLEAN-BOT", "User", "created")]
    [TestCase(132, "helper-app[bot]", "Bot", "created")]
    [TestCase(133, WebhookDelivery.Maintainer, "User", "edited")]
    [TestCase(134, WebhookDelivery.Maintainer, "User", "deleted")]
    public async Task IssueComment_WhenSelfBotOrNotCreated_ShouldBeIgnored(int number, string author, string authorType, string action)
    {
        var result = await DeliverAsync(WebhookDelivery.GithubIssueComment(GithubSecret, "LGTM", author, "OWNER", number, authorType: authorType, action: action));

        result.Outcome.ShouldBe(WebhookOutcome.Ignored);
        CallsFor(number).ShouldBeEmpty();
    }

    [Test]
    public async Task IssueComment_WhenDeliveredTwice_ShouldRouteOnce()
    {
        var delivery = WebhookDelivery.GithubIssueComment(GithubSecret, "LGTM", WebhookDelivery.IssueAuthor, "NONE", 140);

        (await DeliverAsync(delivery)).Outcome.ShouldBe(WebhookOutcome.Processed);
        (await DeliverAsync(delivery)).Outcome.ShouldBe(WebhookOutcome.Duplicate);

        CallsFor(140).ShouldHaveSingleItem();
    }

    private IReadOnlyList<RecordingIssueHandlers.IssueCall> CallsFor(int number)
        => [.. _handlers.Calls.Where(c => c.Event switch
        {
            IssueEvent issue => issue.Issue.Number == number,
            IssueCommentEvent comment => comment.Issue.Number == number,
            _ => false,
        })];
}
