using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Auth;
using ShipLean.TestSupport;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Auth;

// GET /api/auth/instance in both sign-in modes; the client's authApi.test.ts types the same fixture.
[TestFixture]
public class InstanceDtoTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    [TestCase("password")]
    [TestCase("proxy")]
    public void InstanceDto_WhenRoundTripped_ShouldMatchFixture(string example)
    {
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "auth", "instance.json");
        var node = JsonNode.Parse(File.ReadAllText(path))![example]!;

        var value = node.Deserialize<InstanceDto>(Options);

        JsonNode.DeepEquals(node, JsonSerializer.SerializeToNode(value, Options)).ShouldBeTrue();
    }
}
