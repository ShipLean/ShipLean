using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Analytics;
using ShipLean.TestSupport;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Analytics;

[TestFixture]
public class WebVitalsDtoTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    private static JsonNode Pinned(string file)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", file)))!;

    [Test]
    public void WebVitalsBatchDto_WhenRoundTripped_ShouldMatchThePinnedFile()
    {
        var document = Pinned("web-vitals-batch.json");

        var dto = document.Deserialize<WebVitalsBatchDto>(Options)!;

        var report = dto.Reports.ShouldHaveSingleItem();
        report.ShouldBe(new WebVitalsReportDto("taskboard", WebVitalsNavigation.Soft, 812.4, 96, 0.02, null, null, "1920x1080"));
        JsonNode.DeepEquals(document, JsonSerializer.SerializeToNode(dto, Options)).ShouldBeTrue();
    }

    [Test]
    public void WebVitalsConfigDto_WhenRoundTripped_ShouldMatchThePinnedFile()
    {
        var document = Pinned("web-vitals-config.json");

        var dto = document.Deserialize<WebVitalsConfigDto>(Options)!;

        dto.Enabled.ShouldBeFalse();
        JsonNode.DeepEquals(document, JsonSerializer.SerializeToNode(dto, Options)).ShouldBeTrue();
    }

    [TestCase(WebVitalsNavigation.Hard, "hard")]
    [TestCase(WebVitalsNavigation.Soft, "soft")]
    public void WebVitalsNavigation_WhenSerialised_ShouldUseTheWireString(WebVitalsNavigation navigation, string wire)
    {
        JsonSerializer.Serialize(navigation, Options).ShouldBe($"\"{wire}\"");
        JsonSerializer.Deserialize<WebVitalsNavigation>($"\"{wire}\"", Options).ShouldBe(navigation);
    }
}
