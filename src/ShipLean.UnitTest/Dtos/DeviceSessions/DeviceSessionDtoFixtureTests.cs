using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.TestSupport;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.DeviceSessions;

// The device-session wire shapes (K3): each fixture, which the core's deviceSessions types also read, round-trips
// through its C# DTO unchanged.
[TestFixture]
public class DeviceSessionDtoFixtureTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    [TestCase("create-sign-in-code-request.json", typeof(CreateSignInCodeRequest))]
    [TestCase("sign-in-code.json", typeof(SignInCodeDto))]
    [TestCase("exchange-sign-in-code-request.json", typeof(ExchangeSignInCodeRequest))]
    [TestCase("device-sign-in.json", typeof(DeviceSignInDto))]
    [TestCase("device-session-current.json", typeof(DeviceSessionDto))]
    [TestCase("device-session-other.json", typeof(DeviceSessionDto))]
    public void DeviceSessionDtoFixtures_WhenRoundTripped_ShouldMatch(string file, Type type)
    {
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "deviceSessions", file);
        var node = JsonNode.Parse(File.ReadAllText(path))!;

        var value = node.Deserialize(type, Options);

        JsonNode.DeepEquals(node, JsonSerializer.SerializeToNode(value, type, Options)).ShouldBeTrue();
    }
}
