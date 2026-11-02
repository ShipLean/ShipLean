using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Retros;
using ShipLean.UnitTest.Dtos.Boards;

namespace ShipLean.UnitTest.Dtos.Retros;

[TestFixture]
public class RetroDtoTests
{
    [TestCase("sprint-retro.json", typeof(SprintRetroDto))]
    [TestCase("retro-search.json", typeof(RetroSearchDto))]
    [TestCase("retro-insights.json", typeof(RetroInsightsDto))]
    [TestCase("retro-themes.json", typeof(RetroThemeListDto))]
    [TestCase("work-item-retros.json", typeof(WorkItemRetrosDto))]
    [TestCase("retro-point.json", typeof(RetroPointDto))]
    public void RoundTrip_WhenEachFixture_ShouldMatch(string file, Type type)
    {
        var document = BoardDtoFixture.Read("retros", file);

        var value = document.Deserialize(type, BoardDtoFixture.Options);

        JsonNode.DeepEquals(document, JsonSerializer.SerializeToNode(value, type, BoardDtoFixture.Options)).ShouldBeTrue(file);
    }

    [Test]
    public void Enums_WhenSerialized_ShouldBeCamelCase()
    {
        Name(RetroCategory.WentBadly).ShouldBe("wentBadly");
        Name(RetroCategory.NotInPlan).ShouldBe("notInPlan");
        Name(RetroRole.SubAgent).ShouldBe("subAgent");
        Name(RetroFailureKind.AssumptionDisproved).ShouldBe("assumptionDisproved");
        Name(RetroThemeStatus.BeingAddressed).ShouldBe("beingAddressed");
        Name(RetroVerdict.SomeEffect).ShouldBe("someEffect");
        Name(RetroVerdict.TooEarly).ShouldBe("tooEarly");
        Name(RetroThemeMode.Auto).ShouldBe("auto");
        Name(RetroCoverage.Waiting).ShouldBe("waiting");
        Name(RetroTone.Good).ShouldBe("good");
        Name(RetroThemeSetBy.Person).ShouldBe("person");
    }

    private static string Name<T>(T value) where T : struct, Enum
        => JsonSerializer.SerializeToNode(value, BoardDtoFixture.Options)!.GetValue<string>();
}
