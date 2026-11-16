using ShipLean.Application.Features.Demos.Reads;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;

namespace ShipLean.UnitTest.Demos;

[TestFixture]
public class SprintDemoStatusRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Requested = Now.AddHours(-2);

    private static SprintDemoFacts Facts(
        bool closed = true,
        bool requested = true,
        string? blocked = null,
        bool live = false,
        DateTimeOffset? delivered = null,
        DateTimeOffset? expires = null,
        string? sessionState = null,
        DateTimeOffset? sessionEnded = null,
        string? endReason = null,
        string? endDetail = null,
        string? message = null)
        => new(closed, requested ? Requested : null, blocked, live, delivered, expires, sessionState, sessionEnded, endReason, endDetail, message);

    [Test]
    public void Decide_WhenNotRequestedAndNotClosed_ShouldBeClosable()
    {
        SprintDemoStatusRule.Decide(Facts(closed: false, requested: false), Now).ShouldBe((SprintDemoStatus.Closable, "Close the sprint to make its demo."));
    }

    [Test]
    public void Decide_WhenNotRequestedAndClosed_ShouldBeNone()
    {
        SprintDemoStatusRule.Decide(Facts(requested: false), Now).ShouldBe((SprintDemoStatus.None, null));
    }

    [Test]
    public void Decide_WhenNotRequested_ShouldBeClosableOrNoneEvenWithBlockedLiveAndDelivered()
    {
        var facts = Facts(closed: false, requested: false, blocked: "x", live: true, delivered: Now);
        SprintDemoStatusRule.Decide(facts, Now).Status.ShouldBe(SprintDemoStatus.Closable);
    }

    [Test]
    public void Decide_WhenBlockedMessage_ShouldBeBlockedWithTheMessage()
    {
        SprintDemoStatusRule.Decide(Facts(blocked: "No agent connection."), Now).ShouldBe((SprintDemoStatus.Blocked, "No agent connection."));
    }

    [Test]
    public void Decide_WhenBlockedAndLiveAndDelivered_ShouldBeBlocked()
    {
        SprintDemoStatusRule.Decide(Facts(blocked: "No agent.", live: true, delivered: Now), Now).Status.ShouldBe(SprintDemoStatus.Blocked);
    }

    [Test]
    public void Decide_WhenLiveSession_ShouldBeWorking()
    {
        SprintDemoStatusRule.Decide(Facts(live: true), Now).ShouldBe((SprintDemoStatus.Working, null));
    }

    [Test]
    public void Decide_WhenLiveAndDeliveredAndExpired_ShouldBeWorking()
    {
        SprintDemoStatusRule.Decide(Facts(live: true, delivered: Now, expires: Now.AddHours(-1)), Now).Status.ShouldBe(SprintDemoStatus.Working);
    }

    [Test]
    public void Decide_WhenDeliveredAfterRequest_ShouldBeReady()
    {
        SprintDemoStatusRule.Decide(Facts(delivered: Requested.AddMinutes(30)), Now).ShouldBe((SprintDemoStatus.Ready, null));
    }

    [Test]
    public void Decide_WhenDeliveredAtTheRequestInstant_ShouldBeReady()
    {
        SprintDemoStatusRule.Decide(Facts(delivered: Requested), Now).Status.ShouldBe(SprintDemoStatus.Ready);
    }

    [Test]
    public void Decide_WhenDeliveredBeforeRequest_ShouldNotBeReady()
    {
        SprintDemoStatusRule.Decide(Facts(delivered: Requested.AddMinutes(-1)), Now).Status.ShouldBe(SprintDemoStatus.Requested);
    }

    [Test]
    public void Decide_WhenDeliveredAndExpiredAndSessionFailed_ShouldBeReady()
    {
        var facts = Facts(delivered: Requested.AddMinutes(5), expires: Now.AddHours(-1), sessionState: "failed", sessionEnded: Now);
        SprintDemoStatusRule.Decide(facts, Now).Status.ShouldBe(SprintDemoStatus.Ready);
    }

    [Test]
    public void Decide_WhenLinkExpiredAndNothingDelivered_ShouldBeFailedWithTheExpiryMessage()
    {
        SprintDemoStatusRule.Decide(Facts(expires: Now.AddMinutes(-1)), Now)
            .ShouldBe((SprintDemoStatus.Failed, "The upload link expired before a video came back."));
    }

    [Test]
    public void Decide_WhenLinkExpiresExactlyNow_ShouldBeFailed()
    {
        SprintDemoStatusRule.Decide(Facts(expires: Now), Now).Status.ShouldBe(SprintDemoStatus.Failed);
    }

    [Test]
    public void Decide_WhenLinkExpiredAndSessionFailed_ShouldSayExpired()
    {
        SprintDemoStatusRule.Decide(Facts(expires: Now.AddMinutes(-1), sessionState: "failed", sessionEnded: Now), Now)
            .Message.ShouldBe("The upload link expired before a video came back.");
    }

    [TestCase("failed")]
    [TestCase("canceled")]
    [TestCase("stale")]
    public void Decide_WhenLatestSessionEndedWithoutAReason_ShouldBeFailedNamingTheState(string state)
    {
        var result = SprintDemoStatusRule.Decide(Facts(expires: Now.AddHours(1), sessionState: state, sessionEnded: Requested.AddMinutes(10)), Now);

        result.ShouldBe((SprintDemoStatus.Failed, $"The agent session ended as {state} before a video came back."));
    }

    [TestCase("failed", AgentEndReasons.DeliveryFailed, "No answer within 5 seconds. (2 tries)", "The agent connection didn't take the job: No answer within 5 seconds. (2 tries)")]
    [TestCase("failed", AgentEndReasons.DeliveryFailed, null, "The agent connection didn't take the job.")]
    [TestCase("failed", AgentEndReasons.AgentError, "Build broke.\nstack trace", "The agent reported an error: Build broke.")]
    [TestCase("stale", AgentEndReasons.Stale, "No activity for 30 minutes since x.", "The agent went quiet for 30 minutes.")]
    [TestCase("canceled", AgentEndReasons.Canceled, null, "The agent session was stopped.")]
    [TestCase("failed", AgentEndReasons.Unknown, null, "The agent session ended before ShipLean recorded why.")]
    public void Decide_WhenLatestSessionEndedWithAReason_ShouldBeFailedWithItsSentence(string state, string reason, string? detail, string message)
    {
        var facts = Facts(expires: Now.AddHours(1), sessionState: state, sessionEnded: Requested.AddMinutes(10), endReason: reason, endDetail: detail);

        SprintDemoStatusRule.Decide(facts, Now).ShouldBe((SprintDemoStatus.Failed, message));
    }

    [TestCase("stale", AgentEndReasons.Stale, null, "Still rendering", "The agent went quiet for 30 minutes. It said: \"Still rendering\"")]
    [TestCase("completed", AgentEndReasons.Completed, null, "I could not render it.", "The agent session ended without uploading a video. It said: \"I could not render it.\"")]
    [TestCase("failed", AgentEndReasons.AgentError, "Build broke.", "Build broke.", "The agent reported an error: Build broke.")]
    [TestCase("failed", AgentEndReasons.DeliveryFailed, null, null, "The agent connection didn't take the job.")]
    public void Decide_WhenEndedWithAReasonAndAMessage_ShouldQuoteItUnlessTheDetailAlreadyShowsIt(string state, string reason, string? detail, string? message, string expected)
    {
        var facts = Facts(expires: Now.AddHours(1), sessionState: state, sessionEnded: Requested.AddMinutes(10), endReason: reason, endDetail: detail, message: message);

        SprintDemoStatusRule.Decide(facts, Now).ShouldBe((SprintDemoStatus.Failed, expected));
    }

    [Test]
    public void Decide_WhenBadSessionEndedBeforeTheRequest_ShouldBeRequested()
    {
        SprintDemoStatusRule.Decide(Facts(sessionState: "failed", sessionEnded: Requested.AddMinutes(-10)), Now).Status.ShouldBe(SprintDemoStatus.Requested);
    }

    // C5 changed this: a completed session with no video after the request used to stay Requested.
    [Test]
    public void Decide_WhenSessionCompletedWithoutVideo_ShouldBeFailed()
    {
        var facts = Facts(expires: Now.AddHours(1), sessionState: "completed", sessionEnded: Now, message: "I could not render the video.\n\nMore detail.");

        SprintDemoStatusRule.Decide(facts, Now)
            .ShouldBe((SprintDemoStatus.Failed, "The agent session ended without uploading a video. It said: \"I could not render the video.\""));
    }

    [Test]
    public void Decide_WhenSessionCompletedAtTheRequestInstant_ShouldBeFailed()
    {
        SprintDemoStatusRule.Decide(Facts(expires: Now.AddHours(1), sessionState: "completed", sessionEnded: Requested), Now)
            .ShouldBe((SprintDemoStatus.Failed, "The agent session ended without uploading a video."));
    }

    [Test]
    public void Decide_WhenSessionCompletedBeforeTheRequest_ShouldStayRequested()
    {
        var facts = Facts(expires: Now.AddHours(1), sessionState: "completed", sessionEnded: Requested.AddSeconds(-1), message: "Done.");

        SprintDemoStatusRule.Decide(facts, Now).ShouldBe((SprintDemoStatus.Requested, null));
    }

    [Test]
    public void Decide_WhenVideoDelivered_ShouldBeReadyEvenIfMessage()
    {
        var facts = Facts(delivered: Requested.AddMinutes(5), sessionState: "completed", sessionEnded: Requested.AddMinutes(6), message: "Here is your demo.");

        SprintDemoStatusRule.Decide(facts, Now).ShouldBe((SprintDemoStatus.Ready, null));
    }

    [Test]
    public void Decide_WhenLinkExpired_ShouldSayLinkExpired()
    {
        var facts = Facts(expires: Now.AddMinutes(-1), sessionState: "completed", sessionEnded: Now, message: "I gave up.");

        SprintDemoStatusRule.Decide(facts, Now).ShouldBe((SprintDemoStatus.Failed, "The upload link expired before a video came back."));
    }

    [TestCase("failed", null, "The agent session ended as failed before a video came back.")]
    [TestCase("canceled", null, "The agent session ended as canceled before a video came back.")]
    [TestCase("stale", null, "The agent session ended as stale before a video came back.")]
    [TestCase("failed", "Claude Code timed out.", "The agent session ended as failed before a video came back. It said: \"Claude Code timed out.\"")]
    [TestCase("canceled", "Stopped by **Joel**.", "The agent session ended as canceled before a video came back. It said: \"Stopped by Joel.\"")]
    [TestCase("stale", "Still rendering", "The agent session ended as stale before a video came back. It said: \"Still rendering\"")]
    public void Decide_WhenSessionFailedCanceledOrStale_ShouldKeepTodaysTextAndAddTheQuote(string state, string? message, string expected)
    {
        var facts = Facts(expires: Now.AddHours(1), sessionState: state, sessionEnded: Requested.AddMinutes(10), message: message);

        SprintDemoStatusRule.Decide(facts, Now).ShouldBe((SprintDemoStatus.Failed, expected));
    }

    [TestCase("completed", "The agent session ended without uploading a video.")]
    [TestCase("failed", "The agent session ended as failed before a video came back.")]
    public void Decide_WhenMessageBlank_ShouldHaveNoQuote(string state, string expected)
    {
        var facts = Facts(expires: Now.AddHours(1), sessionState: state, sessionEnded: Now, message: " \n\t ");

        SprintDemoStatusRule.Decide(facts, Now).ShouldBe((SprintDemoStatus.Failed, expected));
    }

    [Test]
    public void Decide_WhenRequestedAndNothingElse_ShouldBeRequested()
    {
        SprintDemoStatusRule.Decide(Facts(expires: Now.AddHours(1)), Now).ShouldBe((SprintDemoStatus.Requested, null));
    }
}
