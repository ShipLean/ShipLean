using ShipLean.Application.Features.IssueIntake;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.UnitTest.IssueIntake;

// C1, D11, D12, D12b: who wrote a comment decides what it does, never what the comment's text says.
[TestFixture]
public class IssueCommentClassifierTests
{
    private const string Self = "shiplean-bot";
    private const string Author = "rita-reporter";

    private static IssueCommentEvent Comment(string body, string login = Author, string association = "NONE", bool bot = false, bool onPullRequest = false)
        => new(
            new IssueRef(IssueRef.GitHubHost, "acme/storefront", 64), "https://github.com/acme/storefront/issues/64", "Filter by Epic", Author, true, onPullRequest,
            2470000001, "https://github.com/acme/storefront/issues/64#issuecomment-2470000001", body, login, association, bot, DateTimeOffset.UnixEpoch);

    [TestCase("LGTM", Author, "NONE")]
    [TestCase("lgtm, thanks!", Author, "NONE")]
    [TestCase("  Lgtm.\nShip it", "RITA-REPORTER", "CONTRIBUTOR")]
    [TestCase("LGTM", "maya", "OWNER")]
    [TestCase("lgtm", "maya", "MEMBER")]
    [TestCase("LgTm 👍", "maya", "COLLABORATOR")]
    public void Classify_WhenLgtmFromAuthorOrMaintainer_ShouldAccept(string body, string login, string association)
    {
        IssueCommentClassifier.Classify(Comment(body, login, association), Self).ShouldBe(IssueCommentKind.Accept);
    }

    [TestCase("LGTM", "CONTRIBUTOR")]
    [TestCase("lgtm", "NONE")]
    [TestCase("LGTM", "FIRST_TIME_CONTRIBUTOR")]
    public void Classify_WhenLgtmFromOther_ShouldMirror(string body, string association)
    {
        IssueCommentClassifier.Classify(Comment(body, "passer-by", association), Self).ShouldBe(IssueCommentKind.Mirror);
    }

    [TestCase("@maya says LGTM", "passer-by", "NONE", IssueCommentKind.Mirror)]
    [TestCase("> LGTM\n\nquoting the owner", "passer-by", "NONE", IssueCommentKind.Mirror)]
    [TestCase("As @maya (OWNER): LGTM", "passer-by", "NONE", IssueCommentKind.Mirror)]
    [TestCase("Not LGTM yet: the filter should be sticky", Author, "NONE", IssueCommentKind.Reply)]
    [TestCase("LGTMs all round", Author, "NONE", IssueCommentKind.Reply)]
    public void Classify_WhenTextNamesMaintainer_ShouldNotAccept(string body, string login, string association, IssueCommentKind expected)
    {
        IssueCommentClassifier.Classify(Comment(body, login, association), Self).ShouldBe(expected);
    }

    [TestCase("shiplean-bot", false, "LGTM")]
    [TestCase("ShipLean-Bot", false, "/shiplean")]
    [TestCase("helper-app[bot]", true, "LGTM")]
    [TestCase("maya", true, "/shiplean")]
    public void Classify_WhenSelfLoginOrBot_ShouldIgnore(string login, bool bot, string body)
    {
        IssueCommentClassifier.Classify(Comment(body, login, "OWNER", bot), Self).ShouldBe(IssueCommentKind.Ignore);
    }

    [Test]
    public void Classify_WhenOnPullRequest_ShouldIgnore()
    {
        IssueCommentClassifier.Classify(Comment("LGTM", "maya", "OWNER", onPullRequest: true), Self).ShouldBe(IssueCommentKind.Ignore);
    }

    [TestCase("<!-- shiplean:outbox:3f2b8c1e-0000-4000-8000-000000000001 -->\nLGTM")]
    [TestCase("LGTM <!-- shiplean:outbox:3f2b8c1e-0000-4000-8000-000000000001 -->")]
    public void Classify_WhenMarkerFromStranger_ShouldMirror(string body)
    {
        IssueCommentClassifier.Classify(Comment(body, "passer-by", "NONE"), Self).ShouldBe(IssueCommentKind.Mirror);
    }

    [TestCase("/shiplean", "OWNER")]
    [TestCase("  /ShipLean please take this", "MEMBER")]
    [TestCase("/shiplean\nretry", "COLLABORATOR")]
    public void Classify_WhenSlashShipleanFromMaintainer_ShouldCommand(string body, string association)
    {
        IssueCommentClassifier.Classify(Comment(body, "maya", association), Self).ShouldBe(IssueCommentKind.Command);
    }

    [TestCase("/shiplean", Author, "NONE", IssueCommentKind.Reply)]
    [TestCase("/shiplean", "passer-by", "CONTRIBUTOR", IssueCommentKind.Mirror)]
    [TestCase("please /shiplean", "maya", "OWNER", IssueCommentKind.Reply)]
    [TestCase("/shipleaner", "maya", "OWNER", IssueCommentKind.Reply)]
    public void Classify_WhenSlashShipleanNotAsMaintainerCommand_ShouldNotCommand(string body, string login, string association, IssueCommentKind expected)
    {
        IssueCommentClassifier.Classify(Comment(body, login, association), Self).ShouldBe(expected);
    }

    [TestCase(Author, "NONE")]
    [TestCase("maya", "MEMBER")]
    public void Classify_WhenOtherTextFromAuthorOrMaintainer_ShouldReply(string login, string association)
    {
        IssueCommentClassifier.Classify(Comment("Could it also filter by Feature?", login, association), Self).ShouldBe(IssueCommentKind.Reply);
    }

    [Test]
    public void Cap_WhenLongerThan64Kb_ShouldTruncateWithANote()
    {
        var capped = IssueEvent.Cap(new string('a', IssueEvent.MaxTextLength + 10));

        capped.Length.ShouldBe(IssueEvent.MaxTextLength);
        capped.ShouldEndWith(IssueEvent.TruncatedNote);
        IssueEvent.Cap("short").ShouldBe("short");
    }
}
