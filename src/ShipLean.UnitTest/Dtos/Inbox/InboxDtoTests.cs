using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Inbox;
using ShipLean.TestSupport;
using ShipLean.UnitTest.Dtos.WorkItems;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Inbox;

// The Inbox fixtures in DtoFixtures/inbox are the wire contract the phone app's core types against. Each must survive the
// round trip through the API's JSON options: absent and null differ, so every field is on the wire.
[TestFixture]
public class InboxDtoTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    private static JsonNode Read(string folder, string name)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", folder, name)))!;

    [Test]
    public void InboxDto_WhenRoundTripped_ShouldMatchFixture()
    {
        WorkItemDtoFixture.SurvivesRoundTrip<InboxDto>(Read("inbox", "inbox.json")).ShouldBeTrue();
    }

    // The fixture covers what the app's rows rely on: every ask kind, two mentions labelled per item, a Story under its
    // Feature, a capped WaitedOnBy and another project's count.
    [Test]
    public void InboxDto_WhenRead_ShouldCoverEveryRowShape()
    {
        var inbox = Read("inbox", "inbox.json").Deserialize<InboxDto>(Options)!;
        var asks = inbox.Items.SelectMany(i => i.Asks).ToList();

        asks.Select(a => a.Kind).Distinct().Order().ShouldBe(Enum.GetValues<InboxAskKind>());
        asks.Count(a => new object?[] { a.Decision, a.Question, a.Mention, a.Status }.Count(p => p != null) == 1).ShouldBe(asks.Count);
        var mentionLabels = inbox.Items.Select(i => string.Join(",", i.Asks.Where(a => a.Kind == InboxAskKind.Mention).Select(a => a.Label)));
        mentionLabels.ShouldContain("@1,@2");
        var featureKeys = inbox.Items.Where(i => i.Type == "feature").Select(i => i.Key).ToHashSet();
        inbox.Items.Count(i => i.Type == "story" && i.UnderKey is { } under && featureKeys.Contains(under)).ShouldBe(1);
        inbox.Items.Count(i => i.WaitedOnBy.Count == 3 && i.WaitedOnByMore == 2).ShouldBe(1);
        inbox.OtherProjects.Count.ShouldBe(1);
        inbox.OpenAsks.ShouldBe(asks.Count);
    }

    [Test]
    public void AnsweredDto_WhenRoundTripped_ShouldMatchFixture()
    {
        var node = Read("inbox", "answered.json");
        WorkItemDtoFixture.SurvivesRoundTrip<AnsweredDto>(node).ShouldBeTrue();

        var answered = node.Deserialize<AnsweredDto>(Options)!;
        answered.Rows.Select(r => r.Next?.Kind).ShouldBe([AnsweredNextKind.PersonReplied, AnsweredNextKind.AgentPickedUp, null]);
    }

    [Test]
    public void MentionReplyRequest_WhenRoundTripped_ShouldMatchFixture()
    {
        WorkItemDtoFixture.SurvivesRoundTrip<MentionReplyRequest>(Read("inbox", "mention-reply.json")).ShouldBeTrue();
    }

    [Test]
    public void QuestionDto_WhenOptionsSet_ShouldRoundTrip()
    {
        var node = Read("asks", "item-asks.story.json");
        WorkItemDtoFixture.SurvivesRoundTrip<ItemAsksDto>(node).ShouldBeTrue();

        var story = node.Deserialize<ItemAsksDto>(Options)!;
        story.Questions.Select(q => q.Options.Count).ShouldBe([2, 0]);
    }

    [Test]
    public void InboxAskKind_WhenSerialised_ShouldBeCamelCaseString()
    {
        Enum.GetValues<InboxAskKind>().Select(k => JsonSerializer.Serialize(k, Options))
            .ShouldBe(["\"decision\"", "\"question\"", "\"mention\"", "\"status\""]);
        Enum.GetValues<AnsweredNextKind>().Select(k => JsonSerializer.Serialize(k, Options))
            .ShouldBe(["\"agentPickedUp\"", "\"personReplied\""]);
    }

    [Test]
    public void InboxRules_ShouldHoldTheDecidedLimits()
    {
        (InboxRules.StatusStaleAfter, InboxRules.LongWaitAfter, InboxRules.AnsweredWindow)
            .ShouldBe((TimeSpan.FromDays(3), TimeSpan.FromDays(2), TimeSpan.FromDays(7)));
        (InboxRules.AnsweredLimit, InboxRules.MaxQuestionOptions, InboxRules.ExcerptLength).ShouldBe((50, 6, 280));
    }
}
