using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using Shouldly;

namespace ShipLean.UnitTest.Dtos.Errors;

[TestFixture]
public class ApiErrorDtoTests
{
    private static readonly JsonSerializerOptions Options = ApiJson.CreateOptions();

    private static JsonNode Fixture(string name)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "errors", "api-error.json")))![name]!;

    [Test]
    public void ApiError_WhenFieldSet_ShouldRoundTripTheFixture()
    {
        var document = Fixture("withField");

        var error = document.Deserialize<ApiError>(Options)!;

        error.Field.ShouldBe("slug");
        JsonNode.DeepEquals(document, JsonSerializer.SerializeToNode(error, Options)).ShouldBeTrue();
    }

    [Test]
    public void ApiError_WhenNoField_ShouldRoundTripTheFixtureWithoutAFieldKey()
    {
        var document = Fixture("withoutField");

        var error = document.Deserialize<ApiError>(Options)!;

        error.Field.ShouldBeNull();
        JsonNode.DeepEquals(document, JsonSerializer.SerializeToNode(error, Options)).ShouldBeTrue();
    }
}
