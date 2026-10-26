using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Users;
using ShipLean.UnitTest.Persisted;

namespace ShipLean.UnitTest.Dtos.Users;

// The pinned me.json is the fixture: its `me` is a MeDto and each `users` entry a UserDto, as the API's serializer writes them.
[TestFixture]
public class UserDtoTests
{
    private static readonly JsonSerializerOptions Api = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static IEnumerable<TestCaseData> UserEntries() =>
        PinnedFixture.Read("me.json")["users"]!.AsArray().Select(user => new TestCaseData(user!.DeepClone()).SetArgDisplayNames(user["status"]!.GetValue<string>()));

    [TestCaseSource(nameof(UserEntries))]
    public void UserDto_WhenRoundTripped_ShouldMatchTheFixture(JsonNode entry)
    {
        var dto = entry.Deserialize<UserDto>(Api);

        dto.ShouldNotBeNull();
        JsonNode.DeepEquals(entry, JsonSerializer.SerializeToNode(dto, Api)).ShouldBeTrue();
    }

    [Test]
    public void UserDto_WhenFixtureRead_ShouldCoverEveryStatus()
    {
        var statuses = UserEntries().Select(c => ((JsonNode)c.Arguments[0]!)["status"]!.GetValue<string>()).ToList();

        statuses.Select(status => Enum.Parse<UserStatus>(status, ignoreCase: true)).ShouldBe([UserStatus.Active, UserStatus.OneTimePassword, UserStatus.Locked, UserStatus.NoPassword, UserStatus.Disabled]);
    }

    [Test]
    public void MeDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        var me = PinnedFixture.Read("me.json")["me"]!;

        var dto = me.Deserialize<MeDto>(Api);

        dto.ShouldNotBeNull();
        dto.Theme.ShouldBe(Theme.Os);
        dto.Density.ShouldBe(Density.Compact);
        JsonNode.DeepEquals(me, JsonSerializer.SerializeToNode(dto, Api)).ShouldBeTrue();
    }
}
