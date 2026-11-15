using System.Text.Json.Nodes;
using ShipLean.TestSupport.Inspection;

namespace ShipLean.UnitTest.Inspection;

// Each expected.json's span attributes are exactly K3's normalisation of its otlp.json: resource merged into the record (the record wins
// a clash), doubles, bools and arrays as strings.
[TestFixture]
public class FixtureNormalisationTests
{
    [Test]
    public void SpanAttributes_WhenNormalisedFromTheOtlp_ShouldEqualTheExpectedFile()
    {
        foreach (var fixture in InspectionFixtures.Synthetic)
        {
            var otlp = JsonNode.Parse(File.ReadAllText(fixture.OtlpPath))!;
            var expected = fixture.Expected.Spans.ToDictionary(s => s.SpanId);
            var seen = 0;
            foreach (var resourceSpans in otlp["resourceSpans"]?.AsArray() ?? [])
            {
                foreach (var span in resourceSpans!["scopeSpans"]![0]!["spans"]!.AsArray())
                {
                    var attributes = OtlpAttributes.Normalise(resourceSpans["resource"]!["attributes"]!.AsArray(), span!["attributes"]!.AsArray());
                    attributes.ShouldBe(expected[span["spanId"]!.GetValue<string>()].Attributes, fixture.Name);
                    seen++;
                }
            }
            seen.ShouldBe(fixture.Expected.Spans.Count, fixture.Name);
        }
    }

    [Test]
    public void ClaudeCodeFixture_ShouldCoverTheNormalisationRules()
    {
        var spans = InspectionFixtures.Get("claude-code-synthetic").Expected.Spans;
        var llm = spans.First(s => s.Attributes.ContainsKey("temperature"));

        llm.Attributes["temperature"].ShouldBe("1.5");
        llm.Attributes["allowed_tools"].ShouldBe("[\"Read\",\"Edit\"]");
        llm.Events.ShouldHaveSingleItem().Name.ShouldBe("first_token");
        spans.First(s => s.Attributes.GetValueOrDefault("tool_name") == "mcp__shiplean__pick_up").Attributes["team"].ShouldBe("infra");
        spans.First(s => s.Attributes.GetValueOrDefault("tool_name") is null).Attributes["team"].ShouldBe("platform");
    }
}
