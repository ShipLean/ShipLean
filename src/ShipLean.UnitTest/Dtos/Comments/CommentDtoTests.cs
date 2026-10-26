using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Comments;
using ShipLean.TestSupport;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Comments;

[TestFixture]
public class CommentDtoTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    [Test]
    public void CommentDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "comments", "comments.json");
        var comments = JsonNode.Parse(File.ReadAllText(path))!["comments"]!;

        var value = comments.Deserialize<List<CommentDto>>(Options);

        JsonNode.DeepEquals(comments, JsonSerializer.SerializeToNode(value, Options)).ShouldBeTrue();
    }
}
