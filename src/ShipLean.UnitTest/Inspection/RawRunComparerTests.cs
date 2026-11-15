using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport.Inspection;

namespace ShipLean.UnitTest.Inspection;

[TestFixture]
public class RawRunComparerTests
{
    private static readonly string[] Keys = ["session.id", "tokens"];

    private static RawRun Run(string tokens, string? parent = "p1", string noise = "x")
        => RawRunJson.ParseRun($$"""
            {"summary":{"runId":"r","sessionId":"r","agent":"a","startedBy":null,"models":[],"startedAt":"2026-11-10T10:00:00Z","endedAt":"2026-11-10T10:00:05Z",
              "spanCount":1,"logCount":0,"errorCount":0,"tokensIn":null,"tokensOut":null,"links":[]},
             "spans":[{"traceId":"t","spanId":"s","parentSpanId":{{(parent is null ? "null" : $"\"{parent}\"")}},"name":"n","kind":"internal",
               "start":"2026-11-10T10:00:00Z","end":"2026-11-10T10:00:05Z","status":"ok","statusMessage":null,
               "attributes":{"session.id":"r","tokens":{{tokens}},"other":"{{noise}}"},"events":[]}],
             "logs":[],"truncated":false}
            """);

    [Test]
    public void Differences_WhenAnIntegerAndItsStringForm_ShouldBeEqual()
    {
        RawRunComparer.Differences(Run("42"), Run("\"42\""), Keys).ShouldBeEmpty();
    }

    [Test]
    public void Differences_WhenTheNumbersDiffer_ShouldReportTheAttribute()
    {
        RawRunComparer.Differences(Run("42"), Run("43"), Keys).ShouldHaveSingleItem().ShouldContain("tokens");
    }

    [Test]
    public void Differences_WhenTheParentSpanDiffers_ShouldReportIt()
    {
        RawRunComparer.Differences(Run("1"), Run("1", parent: "p2"), Keys).ShouldHaveSingleItem().ShouldContain("parent");
    }

    [Test]
    public void Differences_WhenAnAttributeIsNotInCompareKeys_ShouldIgnoreIt()
    {
        RawRunComparer.Differences(Run("1", noise: "a"), Run("1", noise: "b"), Keys).ShouldBeEmpty();
    }

    [Test]
    public void Differences_WhenTheActualRunWasRebasedInTime_ShouldStillMatch()
    {
        var expected = Run("1");
        var shifted = expected with
        {
            Summary = expected.Summary with { StartedAt = expected.Summary.StartedAt.AddDays(3), EndedAt = expected.Summary.EndedAt.AddDays(3) },
            Spans = [.. expected.Spans.Select(s => s with { Start = s.Start.AddDays(3), End = s.End.AddDays(3) })],
        };

        RawRunComparer.Differences(expected, shifted, Keys).ShouldBeEmpty();
    }

    [Test]
    public void Differences_WhenEachSyntheticFixtureIsComparedWithItself_ShouldBeEmpty()
    {
        InspectionFixtures.Synthetic.Select(f => f.Name).ShouldBe(["claude-code-synthetic", "codex-cli-synthetic", "cursor-synthetic"]);
        foreach (var fixture in InspectionFixtures.Synthetic)
        {
            RawRunComparer.Differences(fixture.Expected, fixture.Expected, fixture.CompareKeys).ShouldBeEmpty(fixture.Name);
        }
    }
}
