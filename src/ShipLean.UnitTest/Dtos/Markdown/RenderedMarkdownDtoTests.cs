using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Markdown;
using ShipLean.TestSupport;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Markdown;

[TestFixture]
public class RenderedMarkdownDtoTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    [Test]
    public void RenderedImages_WhenRoundTripped_ShouldMatchTheFixture()
    {
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "markdown", "rendered-images.json");
        var fixture = JsonNode.Parse(File.ReadAllText(path))!;

        var attachments = fixture["attachments"].Deserialize<List<AttachmentDto>>(Options);
        var rendered = fixture["rendered"].Deserialize<RenderedMarkdownDto>(Options);

        JsonNode.DeepEquals(fixture["attachments"], JsonSerializer.SerializeToNode(attachments, Options)).ShouldBeTrue();
        JsonNode.DeepEquals(fixture["rendered"], JsonSerializer.SerializeToNode(rendered, Options)).ShouldBeTrue();
    }
}
