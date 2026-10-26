using System.Reflection;
using System.Text.RegularExpressions;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Seed;

[TestFixture]
public class SeedIdsTests
{
    [Test]
    public void SeedIds_ShouldMatchSeedIdsTs()
    {
        var ts = Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.Client", "src", "seedIds.ts")));
        var cs = new Dictionary<string, string>();
        Collect(typeof(SeedIds), "", cs);

        cs.Count.ShouldBeGreaterThan(250);
        ts.OrderBy(p => p.Key, StringComparer.Ordinal).ShouldBe(cs.OrderBy(p => p.Key, StringComparer.Ordinal));
    }

    [Test]
    public void SeedIds_ShouldUseTheValidV4LayoutExceptJoel()
    {
        var cs = new Dictionary<string, string>();
        Collect(typeof(SeedIds), "", cs);

        foreach (var (key, value) in cs.Where(p => Guid.TryParse(p.Value, out _) && p.Key != "users.joel"))
        {
            value.ShouldMatch("^[0-9a-f]{8}-0000-4000-80[0-9]{2}-[0-9]{12}$", key);
        }
        cs.Values.Where(v => Guid.TryParse(v, out _)).Distinct().Count().ShouldBe(cs.Values.Count(v => Guid.TryParse(v, out _)));
    }

    // Flattens nested static classes to "area.nested.name" (camelCase) -> value.
    private static void Collect(Type type, string prefix, Dictionary<string, string> into)
    {
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            into[prefix + Camel(field.Name)] = field.GetValue(null)?.ToString() ?? field.GetRawConstantValue()!.ToString()!;
        }
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
        {
            Collect(nested, prefix + Camel(nested.Name) + ".", into);
        }
    }

    // Reads `name: {` / `name: '...'` lines of the object literal into the same flattened keys.
    private static Dictionary<string, string> Parse(string source)
    {
        var result = new Dictionary<string, string>();
        var path = new Stack<string>();
        foreach (var line in source.Split('\n').Select(l => l.Trim()))
        {
            var open = Regex.Match(line, @"^(\w+): \{$");
            var entry = Regex.Match(line, @"^(\w+): '([^']*)',$");
            if (open.Success)
            {
                path.Push(open.Groups[1].Value);
            }
            else if (entry.Success)
            {
                result[string.Concat(path.Reverse().Select(p => p + ".")) + entry.Groups[1].Value] = entry.Groups[2].Value;
            }
            else if (line.StartsWith('}') && path.Count > 0)
            {
                path.Pop();
            }
        }
        return result;
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
