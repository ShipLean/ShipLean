using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Links;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Dtos.Links;

[TestFixture]
public class LinkCardDtoTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    private static JsonNode Fixture(string name)
    {
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "links", "link-cards.json");
        return JsonNode.Parse(File.ReadAllText(path))![name]!;
    }

    [Test]
    public void LinkCards_ShouldMatchFixtureShapes()
    {
        var linkCards = Fixture("linkCards");

        var value = linkCards.Deserialize<LinkCardsDto>(Options);

        value!.Code.ShouldNotBeEmpty();
        value.Pages.ShouldNotBeEmpty();
        value.WorkItems.ShouldNotBeEmpty();
        JsonNode.DeepEquals(linkCards, JsonSerializer.SerializeToNode(value, Options)).ShouldBeTrue();
    }

    [TestCase("preview")]
    [TestCase("privatePreview")]
    public void LinkPreviewDto_WhenRoundTripped_ShouldMatchTheFixture(string name)
    {
        var preview = Fixture(name);

        var value = preview.Deserialize<LinkPreviewDto>(Options);

        JsonNode.DeepEquals(preview, JsonSerializer.SerializeToNode(value, Options)).ShouldBeTrue();
    }

    [Test]
    public void LinkCards_WhenFixtureGroupsAreRead_ShouldKeepCodeAndPagesApart()
    {
        var value = Fixture("linkCards").Deserialize<LinkCardsDto>(Options)!;

        value.Code.ShouldAllBe(c => c.Group == LinkGroup.Code && c.Kind != LinkKind.Page);
        value.Pages.ShouldAllBe(c => c.Group == LinkGroup.Page && c.Kind == LinkKind.Page);
    }
}
