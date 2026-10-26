using System.Reflection;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi;

namespace ShipLean.UnitTest.Architecture.Mcp;

// Every property of a type deriving McpResult carries [JsonPropertyName] with a snake_case name, because the web
// serializer options would write a multi-word name camelCase. Scans the WebApi and Contracts assemblies. The same holds for
// every record a result or a tool argument reaches through its properties (list elements and nested records included).
[TestFixture]
public class McpResultWireNameTests
{
    [Test]
    public void McpResults_ShouldNameEveryPropertyInSnakeCase()
    {
        var violations = Violations(typeof(ApiMarker).Assembly).Concat(Violations(typeof(McpResult).Assembly)).ToList();

        violations.ShouldBeEmpty();
    }

    [Test]
    public void Guard_WhenResultPropertyHasNoSnakeCaseName_ShouldFail()
    {
        Violations(typeof(McpResultWireNameTests).Assembly, type => type.DeclaringType == typeof(McpResultWireNameTests)).ShouldBe(
            [$"{typeof(BadResult).FullName}.CamelProperty", $"{typeof(BadResult).FullName}.Unnamed"], ignoreOrder: true);
    }

    [Test]
    public void WireNames_NestedRecords_ShouldBeSnakeCase()
    {
        var results = new[] { typeof(ApiMarker).Assembly, typeof(McpResult).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type != typeof(McpResult) && typeof(McpResult).IsAssignableFrom(type));
        var arguments = typeof(ApiMarker).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType));

        NestedViolations(results.Concat(arguments)).ShouldBeEmpty();
    }

    [Test]
    public void Guard_WhenNestedRecordPropertyHasNoSnakeCaseName_ShouldFail()
    {
        NestedViolations([typeof(NestingResult)]).ShouldBe(
            [$"{typeof(BadNested).FullName}.Unnamed", $"{typeof(DeeperNested).FullName}.CamelName"], ignoreOrder: true);
    }

    [Test]
    public void Guard_WhenArgumentOnlyRecordHasNoSnakeCaseName_ShouldFail()
    {
        var arguments = typeof(ArgumentTools).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType));

        NestedViolations(arguments).ShouldBe([$"{typeof(BadArgument).FullName}.Unnamed"]);
    }

    private static IEnumerable<string> Violations(Assembly assembly, Func<Type, bool>? include = null)
        => assembly.GetTypes()
            .Where(type => type != typeof(McpResult) && typeof(McpResult).IsAssignableFrom(type) && (include?.Invoke(type) ?? true))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => !IsSnakeCase(property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
                .Select(property => $"{type.FullName}.{property.Name}"));

    // Walks from the roots through property types (arrays and generic arguments unwrapped) and checks each ShipLean record
    // it reaches that is not itself a result (McpResults_ShouldNameEveryPropertyInSnakeCase checks those).
    private static List<string> NestedViolations(IEnumerable<Type> roots)
    {
        var seen = new HashSet<Type>();
        var pending = new Queue<Type>(roots);
        var violations = new List<string>();
        while (pending.TryDequeue(out var type))
        {
            if (type.IsArray)
            {
                pending.Enqueue(type.GetElementType()!);
                continue;
            }
            if (type.IsGenericType)
            {
                type.GetGenericArguments().ToList().ForEach(pending.Enqueue);
                if (!IsRecord(type))
                {
                    continue;
                }
            }
            if (!IsRecord(type) || !seen.Add(type))
            {
                continue;
            }

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetCustomAttribute<JsonIgnoreAttribute>() is null)
                .ToList();
            properties.ForEach(property => pending.Enqueue(property.PropertyType));
            if (!typeof(McpResult).IsAssignableFrom(type))
            {
                violations.AddRange(properties
                    .Where(property => !IsSnakeCase(property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
                    .Select(property => $"{type.FullName}.{property.Name}"));
            }
        }
        return violations;
    }

    private static bool IsRecord(Type type)
        => type.IsClass && (type.Namespace ?? string.Empty).StartsWith("ShipLean", StringComparison.Ordinal) && type.GetMethod("<Clone>$") is not null;

    private static bool IsSnakeCase(string? name)
        => name is { Length: > 0 } && name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_') && char.IsAsciiLetterLower(name[0]);

    // Fixtures: a result that breaks the rule on two properties and keeps it on one. Never returned by a tool.
    internal sealed record BadResult(
        [property: JsonPropertyName("fine_name")] string FineName,
        [property: JsonPropertyName("camelProperty")] string CamelProperty,
        string Unnamed) : McpResult(null, "https://example.test");

    // A tool whose parameter type is a record no result reaches, with a property lacking its name.
    internal static class ArgumentTools
    {
        [McpServerTool(Name = "fixture_argument")]
        public static string Run(IReadOnlyList<BadArgument> items) => items.Count.ToString();
    }

    internal sealed record BadArgument(string Unnamed);

    // A result whose own names keep the rule and whose nested records break it: one directly, one a level deeper.
    internal sealed record NestingResult(
        [property: JsonPropertyName("nested")] IReadOnlyList<BadNested> Nested) : McpResult(null, "https://example.test");

    internal sealed record BadNested(
        [property: JsonPropertyName("deeper")] DeeperNested? Deeper,
        string Unnamed);

    internal sealed record DeeperNested(
        [property: JsonPropertyName("fine_name")] string FineName,
        [property: JsonPropertyName("camelName")] string CamelName);
}
