using CsCheck;
using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Application.Features.Mcp.Planning.Waves;

namespace ShipLean.UnitTest.Mcp.Planning;

[TestFixture]
public class PlanTagsTests
{
    private static readonly Gen<string> Words = Gen.String[Gen.Char["abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_./:"], 1, 20]
        .Where(word => !string.IsNullOrWhiteSpace(word));

    private static readonly Gen<WaveLabel> Waves = Gen.Int[0, 30].Array[1, 3]
        .Select(parts => WaveLabel.Parse(string.Join('.', parts)));

    private static readonly Gen<PlanTaskTags> AnyTags =
        Gen.Select(Waves.Null(), Words.Null(), Words.Null(), Gen.Int[0, 500].Nullable(), Words.Null(), Gen.Bool)
            .Select(t => new PlanTaskTags(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5, t.Item6));

    [Test]
    public void PlanTags_FormatThenParse_ShouldRoundTrip()
    {
        AnyTags.Sample(tags => PlanTags.Parse(PlanTags.Format(tags)).ShouldBe(tags));
    }

    [Test]
    public void Format_WhenEverythingSet_ShouldWriteEachTagInOrder()
    {
        var tags = new PlanTaskTags(WaveLabel.Parse("0.1"), "F", "strong", 55, "feat/2-feature-editor", Orphan: true);

        PlanTags.Format(tags).ShouldBe(["wave:0.1", "lane:F", "model:strong", "ctx:55k", "branch:feat/2-feature-editor", "orphan"]);
    }

    [Test]
    public void Parse_WhenMixedWithOtherTags_ShouldReadOnlyThePlansAndTheFirstOfEach()
    {
        var parsed = PlanTags.Parse(["agent", "wave:soon", "wave:1.1", "wave:2", "ctx:lots", "ctx:40k", "lane:", "lane:A", "orphaned"]);

        parsed.ShouldBe(new PlanTaskTags(WaveLabel.Parse("1.1"), "A", ContextK: 40));
    }

    [TestCase("wave:1", true)]
    [TestCase("lane:F", true)]
    [TestCase("model:fast", true)]
    [TestCase("ctx:40k", true)]
    [TestCase("branch:feat/x", true)]
    [TestCase("orphan", true)]
    [TestCase("agent", false)]
    [TestCase("orphaned", false)]
    [TestCase("Wave:1", false)]
    public void IsPlanTag_ShouldMatchOnlyThePlansPrefixes(string tag, bool expected)
    {
        PlanTags.IsPlanTag(tag).ShouldBe(expected);
    }

    [Test]
    public void WaveLabel_ShouldSortAsDottedNumbers()
    {
        string[] shuffled = ["2", "1.10", "0.2", "1.1", "1", "0.1", "1.9", "1.0"];

        shuffled.Select(WaveLabel.Parse).Order().Select(label => label.Text).ShouldBe(["0.1", "0.2", "1", "1.0", "1.1", "1.9", "1.10", "2"]);
        WaveLabel.Parse("01.1").ShouldBe(WaveLabel.Parse("1.1"));
        (WaveLabel.Parse("0.2") < WaveLabel.Parse("1")).ShouldBeTrue();
    }

    [TestCase("")]
    [TestCase("1.")]
    [TestCase(".1")]
    [TestCase("1..2")]
    [TestCase("v1")]
    [TestCase("1 ")]
    [TestCase("-1")]
    [TestCase("9999999999")]
    public void WaveLabel_WhenNotDottedNumbers_ShouldNotParse(string text)
    {
        WaveLabel.TryParse(text, out _).ShouldBeFalse();
    }
}
