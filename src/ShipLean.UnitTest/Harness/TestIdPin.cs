using System.Text.RegularExpressions;

namespace ShipLean.UnitTest.Harness;

// Pairs each client testIds.ts with its TestSupport/TestIds/<Area>TestIds.cs and compares them.
//
// The TypeScript side is an `as const` object named <area>TestIds with one `key: 'value',` entry per line: single
// quotes, no computed keys or spreads; a line starting with `//` is a comment (a block heading). The C# side is a public
// static class <Area>TestIds whose const names are the keys with the first letter upper-cased and nothing else changed.
// Whitespace and blank lines do not matter.
//
// Pairs: src/ShipLean.Client/src/features/<area>/testIds.ts <-> TestIds/<Area>TestIds.cs, plus the fixed pairs
// src/testIds.ts <-> AppTestIds.cs (appTestIds) and src/components/testIds.ts <-> ComponentTestIds.cs (componentTestIds).
public static partial class TestIdPin
{
    public sealed record Pair(string Name, string? TsPath, string? CsPath, string ExportName, string ClassName, string? Problem = null);

    public static IReadOnlyList<Pair> DiscoverPairs(string repoRoot)
    {
        var clientSrc = Path.Combine(repoRoot, "src", "ShipLean.Client", "src");
        var csDirectory = Path.Combine(repoRoot, "src", "ShipLean.TestSupport", "TestIds");
        var pairs = new List<Pair>();

        var featuresDirectory = Path.Combine(clientSrc, "features");
        if (Directory.Exists(featuresDirectory))
        {
            foreach (var folder in Directory.GetDirectories(featuresDirectory).Order(StringComparer.Ordinal))
            {
                var tsPath = Path.Combine(folder, "testIds.ts");
                if (File.Exists(tsPath))
                {
                    var area = Path.GetFileName(folder);
                    pairs.Add(Make(area, area + "TestIds", UpperFirst(area) + "TestIds", tsPath, csDirectory));
                }
            }
        }
        pairs.Add(Make("app", "appTestIds", "AppTestIds", Path.Combine(clientSrc, "testIds.ts"), csDirectory));
        pairs.Add(Make("components", "componentTestIds", "ComponentTestIds", Path.Combine(clientSrc, "components", "testIds.ts"), csDirectory));

        // A C# class with no TS file also fails.
        if (Directory.Exists(csDirectory))
        {
            foreach (var csPath in Directory.GetFiles(csDirectory, "*TestIds.cs").Order(StringComparer.Ordinal))
            {
                var className = Path.GetFileNameWithoutExtension(csPath);
                if (pairs.All(p => p.ClassName != className))
                {
                    pairs.Add(new Pair(className, null, csPath, string.Empty, className));
                }
            }
        }
        // A testIds.ts anywhere else would never be pinned, so it fails instead of being skipped.
        if (Directory.Exists(clientSrc))
        {
            foreach (var tsPath in Directory.EnumerateFiles(clientSrc, "testIds.ts", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                if (pairs.All(p => p.TsPath != tsPath))
                {
                    pairs.Add(new Pair(tsPath, tsPath, null, string.Empty, string.Empty,
                        $"{tsPath}: testIds.ts belongs in features/<area>/, src/ or src/components/"));
                }
            }
        }
        return pairs.Where(p => p.TsPath != null || p.CsPath != null).ToList();
    }

    // Every way the pair differs; empty when they match.
    public static IReadOnlyList<string> Compare(Pair pair)
    {
        if (pair.Problem != null)
        {
            return [pair.Problem];
        }
        if (pair.TsPath == null)
        {
            return [$"{pair.ClassName}: no TypeScript file for {pair.CsPath}"];
        }
        if (pair.CsPath == null)
        {
            return [$"{pair.ExportName}: no C# class {pair.ClassName} for {pair.TsPath}"];
        }
        var problems = new List<string>();
        var ts = ReadTs(File.ReadAllText(pair.TsPath), pair.ExportName, problems);
        var cs = ReadCs(File.ReadAllText(pair.CsPath), pair.ClassName, problems);
        foreach (var (key, value) in ts)
        {
            if (!cs.TryGetValue(key, out var csValue))
            {
                problems.Add($"{pair.ClassName}: missing const {key} for '{value}'");
            }
            else if (csValue != value)
            {
                problems.Add($"{pair.ClassName}.{key}: C# has \"{csValue}\", TypeScript has '{value}'");
            }
        }
        problems.AddRange(cs.Keys.Where(k => !ts.ContainsKey(k)).Select(k => $"{pair.ClassName}: const {k} has no key in {pair.ExportName}"));
        return problems;
    }

    // Keys already upper-cased on the first letter, so they compare directly with the C# const names.
    private static Dictionary<string, string> ReadTs(string text, string exportName, List<string> problems)
    {
        var result = new Dictionary<string, string>();
        var block = Regex.Match(text, @"export\s+const\s+" + Regex.Escape(exportName) + @"\s*=\s*\{(?<body>.*?)\}\s*as\s+const", RegexOptions.Singleline);
        if (!block.Success)
        {
            problems.Add($"export const {exportName} = {{ ... }} as const not found");
            return result;
        }
        foreach (var line in block.Groups["body"].Value.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal)))
        {
            var entry = TsEntry().Match(line);
            if (!entry.Success)
            {
                problems.Add($"{exportName}: line is not `key: 'value',`: {line}");
            }
            else if (!result.TryAdd(UpperFirst(entry.Groups["key"].Value), entry.Groups["value"].Value))
            {
                problems.Add($"{exportName}: key {entry.Groups["key"].Value} appears twice");
            }
        }
        return result;
    }

    private static Dictionary<string, string> ReadCs(string text, string className, List<string> problems)
    {
        var result = new Dictionary<string, string>();
        if (!Regex.IsMatch(text, @"public\s+static\s+class\s+" + Regex.Escape(className) + @"\b"))
        {
            problems.Add($"public static class {className} not found");
            return result;
        }
        foreach (Match match in CsConst().Matches(text))
        {
            if (!result.TryAdd(match.Groups["name"].Value, match.Groups["value"].Value))
            {
                problems.Add($"{className}: const {match.Groups["name"].Value} appears twice");
            }
        }
        return result;
    }

    private static Pair Make(string name, string exportName, string className, string tsPath, string csDirectory)
    {
        var csPath = Path.Combine(csDirectory, className + ".cs");
        return new Pair(name, File.Exists(tsPath) ? tsPath : null, File.Exists(csPath) ? csPath : null, exportName, className);
    }

    private static string UpperFirst(string value) => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    [GeneratedRegex(@"^(?<key>[A-Za-z_]\w*)\s*:\s*'(?<value>[^']*)'\s*,?$")]
    private static partial Regex TsEntry();

    [GeneratedRegex(@"public\s+const\s+string\s+(?<name>\w+)\s*=\s*""(?<value>[^""]*)""\s*;")]
    private static partial Regex CsConst();
}
