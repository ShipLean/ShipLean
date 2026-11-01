using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Analytics;
using ShipLean.TestSupport;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Analytics;

[TestFixture]
public class AnalyticsDtoTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    private static JsonNode Pinned(string file)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", file)))!;

    [Test]
    public void AnalyticsSettingsDto_WhenRoundTripped_ShouldMatchThePinnedFile()
    {
        var document = Pinned("analytics-settings.json");

        var dto = document.Deserialize<AnalyticsSettingsDto>(Options)!;

        dto.Provider.ShouldBe(AnalyticsProvider.Umami);
        dto.Host.ShouldBe("https://umami.example.com");
        JsonNode.DeepEquals(document, JsonSerializer.SerializeToNode(dto, Options)).ShouldBeTrue();
    }

    [Test]
    public void AnalyticsTestResultDto_WhenRoundTripped_ShouldMatchThePinnedFile()
    {
        var document = Pinned("analytics-test-result.json");

        var dto = document.Deserialize<AnalyticsTestResultDto>(Options)!;

        dto.Ok.ShouldBeFalse();
        JsonNode.DeepEquals(document, JsonSerializer.SerializeToNode(dto, Options)).ShouldBeTrue();
    }

    [TestCase(AnalyticsProvider.None, "none")]
    [TestCase(AnalyticsProvider.Umami, "umami")]
    [TestCase(AnalyticsProvider.PostHog, "posthog")]
    public void AnalyticsProvider_WhenSerialised_ShouldUseTheWireString(AnalyticsProvider provider, string wire)
    {
        var json = JsonSerializer.SerializeToNode(new AnalyticsSettingsDto(provider, null, null, false), Options)!;

        json["provider"]!.GetValue<string>().ShouldBe(wire);
        JsonSerializer.Deserialize<AnalyticsProvider>($"\"{wire}\"", Options).ShouldBe(provider);
    }

    [Test]
    public void AnalyticsProvider_WhenCamelCaseNameIsSent_ShouldBeRejected()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<AnalyticsProvider>("\"postHog\"", Options));
    }

    [Test]
    public void AnalyticsProps_ShouldMatchTheStandardKeys()
    {
        var keys = typeof(AnalyticsProps).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.Name is not (nameof(AnalyticsProps.PersonKind) or nameof(AnalyticsProps.PersonUserId) or nameof(AnalyticsProps.PersonAgentName)
                or nameof(AnalyticsProps.PersonAgentHost) or nameof(AnalyticsProps.PersonAgentVerified) or nameof(AnalyticsProps.PersonName) or nameof(AnalyticsProps.PersonEmail)))
            .Select(f => (string)f.GetRawConstantValue()!);

        keys.Order().ShouldBe(
            new[]
            {
                "surface", "actor_kind", "user_id", "agent_id", "agent_name", "agent_host", "agent_verified",
                "project_id", "project_key", "project_count", "outcome", "error_code", "duration_ms", "source",
                "shiplean_version", "instance_host",
            }.Order());
    }

    [Test]
    public void AnalyticsProps_ShouldMatchThePersonKeys()
    {
        new[] { AnalyticsProps.PersonKind, AnalyticsProps.PersonUserId, AnalyticsProps.PersonAgentName, AnalyticsProps.PersonAgentHost, AnalyticsProps.PersonAgentVerified, AnalyticsProps.PersonName, AnalyticsProps.PersonEmail }
            .ShouldBe(["kind", "user_id", "agent_name", "agent_host", "agent_verified", "name", "email"]);
    }
}
