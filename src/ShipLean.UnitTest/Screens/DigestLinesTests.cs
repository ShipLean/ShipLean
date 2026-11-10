using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.FeaturesOverview;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;
using ShipLean.Contracts.WorkItems.Reads.Changes;
using ShipLean.Core;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Screens;

// DigestLines: one change becomes the digest lines it deserves. Revisions give a line for each of state, health and the status line
// they changed; people's comments give their text; agents' work that gave none of those gives one agentWork line.
[TestFixture]
public class DigestLinesTests
{
    private static readonly FakeTimeProvider Clock = new(TestDates.Today.AddHours(12));
    private static readonly DateTimeOffset At = TestDates.Today.AddHours(9);
    private static readonly UserRef Tom = new(SeedIds.Users.Tom, "Tom Okafor", "TO", "#16a089", null);

    private static FieldChange Change(string field, object? from, object? to, string? fromLabel = null, string? toLabel = null) =>
        new(field, JsonSerializer.SerializeToElement(from), JsonSerializer.SerializeToElement(to), fromLabel, toLabel);

    private static ChangeEntryDto Revision(string? agent, params FieldChange[] fields) =>
        new(Guid.NewGuid(), "SHIP-101", "Checkout v2", ChangeKind.Revision, SeedIds.Users.Tom, "Tom Okafor", At, fields, null, 2, agent, "feature");

    private static ChangeEntryDto Comment(string body, string? agent = null) =>
        new(Guid.NewGuid(), "SHIP-101", "Checkout v2", ChangeKind.Comment, SeedIds.Users.Tom, "Tom Okafor", At, null, body, null, agent, "feature");

    [Test]
    public void Lines_WhenRevisionChangesStateAndHealth_ShouldGiveTwoLines()
    {
        var entry = Revision(null,
            Change(FieldIds.Title, "Old", "Checkout v2"),
            Change(FieldIds.State, Guid.NewGuid(), Guid.NewGuid(), "Active", "Closed"),
            Change(FieldIds.Health, "onTrack", "atRisk"));

        var lines = DigestLines.Of(entry, Tom, Clock);

        lines.Select(l => (l.Kind, l.Text)).ShouldBe([(DigestLineKind.State, "Active → Closed"), (DigestLineKind.Health, "On track → At risk")]);
        lines.ShouldAllBe(l => l.By == Tom && l.AgentName == null && l.At == At && l.AtLabel == DisplayDate.Relative(At, Clock));
    }

    [Test]
    public void Lines_WhenHealthFirstSet_ShouldGiveJustTheNewHealth()
    {
        var lines = DigestLines.Of(Revision(null, Change(FieldIds.Health, null, "blocked")), Tom, Clock);

        lines.Single().Text.ShouldBe("Blocked");
    }

    [Test]
    public void Lines_WhenStatusLineChanged_ShouldGiveTheNewLine()
    {
        var lines = DigestLines.Of(Revision(null, Change(FieldIds.StatusLine, "Was this", "Waiting on the fraud check API.")), Tom, Clock);

        var line = lines.ShouldHaveSingleItem();
        line.Kind.ShouldBe(DigestLineKind.StatusLine);
        line.Text.ShouldBe("Waiting on the fraud check API.");
    }

    [Test]
    public void Lines_WhenStatusLineCleared_ShouldGiveNoLine()
    {
        DigestLines.Of(Revision(null, Change(FieldIds.StatusLine, "Was this", null)), Tom, Clock).ShouldBeEmpty();
    }

    [Test]
    public void Lines_WhenAgentCommentOrRevision_ShouldGiveAgentWork()
    {
        var comment = DigestLines.Of(Comment("Pushed 3 commits", "Claude Code"), Tom, Clock).ShouldHaveSingleItem();
        var revision = DigestLines.Of(Revision("Claude Code", Change(FieldIds.Points, null, 3)), Tom, Clock).ShouldHaveSingleItem();
        var stateByAgent = DigestLines.Of(Revision("Claude Code", Change(FieldIds.State, Guid.NewGuid(), Guid.NewGuid(), "Active", "Closed")), Tom, Clock).ShouldHaveSingleItem();

        (comment.Kind, comment.Text, comment.AgentName).ShouldBe((DigestLineKind.AgentWork, "Claude Code commented", "Claude Code"));
        (revision.Kind, revision.Text, revision.AgentName).ShouldBe((DigestLineKind.AgentWork, "Claude Code changed Story points", "Claude Code"));
        (stateByAgent.Kind, stateByAgent.Text, stateByAgent.AgentName).ShouldBe((DigestLineKind.State, "Active → Closed", "Claude Code"));
    }

    [Test]
    public void Lines_WhenPersonComments_ShouldGiveCommentCutOnAWord()
    {
        var short1 = DigestLines.Of(Comment("Vault tokens **expire** after a year;\nwe refresh them on use."), Tom, Clock).ShouldHaveSingleItem();
        var body = string.Join(' ', Enumerable.Range(1, 120).Select(i => $"word{i}"));

        var long1 = DigestLines.Of(Comment(body), Tom, Clock).ShouldHaveSingleItem();

        short1.Kind.ShouldBe(DigestLineKind.Comment);
        short1.Text.ShouldBe("Vault tokens expire after a year; we refresh them on use.");
        long1.Text.ShouldEndWith("…");
        long1.Text.Length.ShouldBeLessThanOrEqualTo(DigestLines.CommentLength + 1);
        var kept = long1.Text[..^1];
        body.ShouldStartWith(kept);
        body[kept.Length].ShouldBe(' ');
        kept.Length.ShouldBeGreaterThan(DigestLines.CommentLength - 12);
    }

    [Test]
    public void Lines_WhenOnlyTitleChangedByAPerson_ShouldGiveNoLine()
    {
        var lines = DigestLines.Of(Revision(null, Change(FieldIds.Title, "Old", "New"), Change(FieldIds.Points, null, 5)), Tom, Clock);

        lines.ShouldBeEmpty();
    }
}
