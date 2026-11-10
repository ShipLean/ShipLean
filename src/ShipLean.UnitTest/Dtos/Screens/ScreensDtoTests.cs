using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Inbox;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Screens;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Dtos.Screens;

// The phone screens' fixtures in DtoFixtures/screens are the wire contract the core types against. Each must survive the
// round trip through the API's JSON options: absent and null differ, so every field is on the wire.
[TestFixture]
public class ScreensDtoTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    private static JsonNode Read(string name)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "screens", name)))!;

    private static bool SurvivesRoundTrip<T>(JsonNode node)
        => JsonNode.DeepEquals(node, JsonSerializer.SerializeToNode(node.Deserialize<T>(Options), Options));

    private static string Name<T>(T value) where T : struct, Enum
        => JsonSerializer.SerializeToNode(value, Options)!.GetValue<string>();

    // The fixture covers what the overview relies on: the four groups in order, Closed folded with a count and no rows,
    // a stale status, asks on a Feature's stories and a Feature under its Epic.
    [Test]
    public void FeaturesOverviewDto_WhenRoundTripped_ShouldMatchFixture()
    {
        var node = Read("features-overview.json");
        SurvivesRoundTrip<FeaturesOverviewDto>(node).ShouldBeTrue();

        var overview = node.Deserialize<FeaturesOverviewDto>(Options)!;
        overview.Groups.Select(g => g.Label).ShouldBe(["Active", "Resolved", "New", "Closed"]);
        var closed = overview.Groups[^1];
        closed.Folded.ShouldBeTrue();
        closed.Rows.ShouldBeEmpty();
        closed.Count.ShouldBeGreaterThan(0);
        var rows = overview.Groups.SelectMany(g => g.Rows).ToList();
        rows.Count(r => r.StatusStale).ShouldBe(1);
        rows.Count(r => r.NeedsYou is { OnChildren: true }).ShouldBe(1);
        rows.Count(r => r.Epic is not null).ShouldBe(1);
    }

    [Test]
    public void FeatureDigestDto_WhenRoundTripped_ShouldMatchFixture()
    {
        var node = Read("feature-digest.json");
        SurvivesRoundTrip<FeatureDigestDto>(node).ShouldBeTrue();

        var digest = node.Deserialize<FeatureDigestDto>(Options)!;
        digest.NeedsYou.Concat(digest.Changed).SelectMany(i => i.Lines).Select(l => l.Kind).Distinct().Order()
            .ShouldBe(Enum.GetValues<DigestLineKind>());
        digest.More.ShouldBeTrue();
        digest.Cursor.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public void BacklogRowDto_WhenNeedsYouSet_ShouldRoundTrip()
    {
        var node = Read("backlog-row-needs.json");
        SurvivesRoundTrip<BacklogRowDto>(node).ShouldBeTrue();

        var row = node.Deserialize<BacklogRowDto>(Options)!;
        row.NeedsYou.ShouldNotBeNull();
        row.BlockedBy.ShouldNotBeEmpty();
    }

    [Test]
    public void RetroPointDto_WhenSupported_ShouldRoundTrip()
    {
        var node = Read("retro-point-supported.json");
        SurvivesRoundTrip<RetroPointDto>(node).ShouldBeTrue();

        var point = node.Deserialize<RetroPointDto>(Options)!;
        point.Supporters.Count.ShouldBe(2);
        point.SupportedByMe.ShouldBeTrue();
    }

    [Test]
    public void WorkItemDto_WhenHealthSet_ShouldRoundTrip()
    {
        var node = Read("work-item-health.json");
        SurvivesRoundTrip<WorkItemDto>(node).ShouldBeTrue();

        var item = node.Deserialize<WorkItemDto>(Options)!;
        item.Health.ShouldNotBeNull();
        item.HealthSet.ShouldNotBeNull();
        item.Rollups.ShouldNotBeEmpty();
        item.NeedsYou.ShouldNotBeNull();
    }

    [Test]
    public void FeatureBoardRowDto_WhenNeedsYouSet_ShouldRoundTrip()
    {
        var node = Read("feature-board-row-needs.json");
        SurvivesRoundTrip<FeatureBoardRowDto>(node).ShouldBeTrue();
        node.Deserialize<FeatureBoardRowDto>(Options)!.NeedsYou.ShouldNotBeNull();
    }

    [Test]
    public void CardDto_WhenHealthSet_ShouldRoundTrip()
    {
        var node = Read("card-health.json");
        SurvivesRoundTrip<CardDto>(node).ShouldBeTrue();
        node.Deserialize<CardDto>(Options)!.Health.ShouldNotBeNull();
    }

    [Test]
    public void FeatureHealth_WhenSerialised_ShouldBeCamelCaseString()
    {
        Enum.GetValues<FeatureHealth>().Select(Name).ShouldBe(["onTrack", "atRisk", "blocked"]);
    }

    [Test]
    public void DigestLineKind_WhenSerialised_ShouldBeCamelCaseString()
    {
        Enum.GetValues<DigestLineKind>().Select(Name).ShouldBe(["state", "health", "statusLine", "comment", "agentWork", "ask"]);
    }

    [Test]
    public void WorkItemChanges_WhenHealthSet_ShouldSerialiseAsFieldSet()
    {
        var set = JsonSerializer.Deserialize<WorkItemChanges>("""{"health":{"value":"blocked"}}""", Options)!;
        set.Health.ShouldNotBeNull().Value.ShouldBe(FeatureHealth.Blocked);
        JsonNode.DeepEquals(JsonSerializer.SerializeToNode(set, Options)!["health"], JsonNode.Parse("""{"value":"blocked"}""")).ShouldBeTrue();

        var cleared = JsonSerializer.Deserialize<WorkItemChanges>("""{"health":{"value":null}}""", Options)!;
        cleared.Health.ShouldNotBeNull().Value.ShouldBeNull();
        var clearedHealth = JsonSerializer.SerializeToNode(cleared, Options)!["health"]!.AsObject();
        clearedHealth.ContainsKey("value").ShouldBeTrue();
        clearedHealth["value"].ShouldBeNull();

        var absent = JsonSerializer.Deserialize<WorkItemChanges>("""{"title":{"value":"New"}}""", Options)!;
        absent.Health.ShouldBeNull();
        JsonSerializer.Deserialize<WorkItemChanges>("{\"health\":null}", Options)!.Health.ShouldBeNull();
    }

    [Test]
    public void FeatureRules_WhenRead_ShouldEqualInboxRulesStatusStaleAfter()
    {
        FeatureRules.StatusStaleAfter.ShouldBe(TimeSpan.FromDays(3));
        InboxRules.StatusStaleAfter.ShouldBe(TimeSpan.FromDays(3));
        FeatureRules.StatusStaleAfter.ShouldBe(InboxRules.StatusStaleAfter);
    }
}
