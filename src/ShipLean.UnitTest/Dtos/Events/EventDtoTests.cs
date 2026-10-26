using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Dtos.Events;

[TestFixture]
public class EventDtoTests
{
    private static JsonObject Fixture()
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "events", "events.json")))!.AsObject();

    [Test]
    public void ProjectEvents_WhenRoundTripped_ShouldMatchTheFixture()
    {
        var options = ApiJson.CreateOptions();
        var examples = Fixture();

        examples.ShouldNotBeEmpty();
        foreach (var (name, example) in examples)
        {
            var value = example.Deserialize<ProjectEvent>(options)!;
            value.GetType().IsAbstract.ShouldBeFalse(name);
            JsonNode.DeepEquals(example, JsonSerializer.SerializeToNode(value, options)).ShouldBeTrue(name);
            example!["type"]!.GetValue<string>().ShouldBe(name);
        }
    }

    [Test]
    public void ProjectEvents_WhenClientIdIsAbsent_ShouldWriteNull()
    {
        var node = JsonSerializer.SerializeToNode<ProjectEvent>(new StatesChanged(Guid.NewGuid()), ApiJson.CreateOptions())!.AsObject();

        node.ContainsKey("clientId").ShouldBeTrue();
        node["clientId"].ShouldBeNull();
    }

    [Test]
    public void EventTypes_ShouldMatchBetweenCSharpAndTypeScript()
    {
        var declared = typeof(ProjectEvent).GetCustomAttributes<JsonDerivedTypeAttribute>().ToList();
        var concrete = typeof(ProjectEvent).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(ProjectEvent))).ToList();

        declared.Select(d => d.DerivedType).Order(Comparer<Type>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name)))
            .ShouldBe(concrete.Order(Comparer<Type>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name))));
        declared.Select(d => (string)d.TypeDiscriminator!).Order(StringComparer.Ordinal)
            .ShouldBe(Fixture().Select(e => e.Key).Order(StringComparer.Ordinal));
    }
}
