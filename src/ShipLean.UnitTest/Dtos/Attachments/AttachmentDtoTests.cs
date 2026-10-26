using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Attachments;
using ShipLean.TestSupport;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Attachments;

[TestFixture]
public class AttachmentDtoTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    [Test]
    public void AttachmentDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "attachments", "attachments.json");
        var attachments = JsonNode.Parse(File.ReadAllText(path))!["attachments"]!;

        var value = attachments.Deserialize<List<AttachmentDto>>(Options);

        JsonNode.DeepEquals(attachments, JsonSerializer.SerializeToNode(value, Options)).ShouldBeTrue();
    }

    [Test]
    public void AttachmentCheck_WhenRoundTripped_ShouldMatchTheFixture()
    {
        var fixture = Fixture("attachment-check.json");

        var request = fixture["request"]!.Deserialize<AttachmentCheckRequest>(Options);
        var response = fixture["response"]!.Deserialize<AttachmentCheckDto>(Options);

        JsonNode.DeepEquals(fixture["request"], JsonSerializer.SerializeToNode(request, Options)).ShouldBeTrue();
        JsonNode.DeepEquals(fixture["response"], JsonSerializer.SerializeToNode(response, Options)).ShouldBeTrue();
    }

    [Test]
    public void AttachmentMarkdown_WhenRoundTripped_ShouldMatchTheFixture()
    {
        var view = Fixture("attachment-markdown.json")["markdownView"]!;

        var value = view.Deserialize<AttachmentMarkdownDto>(Options);

        JsonNode.DeepEquals(view, JsonSerializer.SerializeToNode(value, Options)).ShouldBeTrue();
    }

    private static JsonNode Fixture(string file)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "attachments", file)))!;
}
