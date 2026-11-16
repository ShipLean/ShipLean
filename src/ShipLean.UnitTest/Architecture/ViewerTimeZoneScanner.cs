using System.Text.RegularExpressions;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Architecture;

// Finds zone code, calls to the old TimeProvider overloads of DisplayDate and ad-hoc date formats in C# source, for
// ViewerTimeZoneTests. Comments and string contents are masked first, so neither is mistaken for code; interpolation
// holes stay code. Arguments are counted by paren depth, so generic type arguments with a comma at depth one would be
// miscounted (no call site has one).
public static partial class ViewerTimeZoneScanner
{
    public const string OldRelative = "DisplayDate.Relative(value, clock)";

    private static readonly string[] ScannedProjects =
        ["ShipLean.Core", "ShipLean.Contracts", "ShipLean.Application", "ShipLean.WebApi", "ShipLean.LocalHost"];

    // Permanently allowed (paths from the repository root):
    // - DisplayDate is where the viewer's zone is applied and every display format lives.
    private const string DisplayDatePath = "src/ShipLean.Core/DisplayDate.cs";

    // - SprintLabels keeps its "d MMM" formats: sprint dates are calendar days with no zone (D8). Zone code there still fails.
    private const string SprintLabelsPath = "src/ShipLean.Core/SprintLabels.cs";

    private static readonly (Regex Pattern, string Token)[] ZoneTokens =
    [
        (LocalTimeZoneRegex(), "LocalTimeZone"),
        (LocalZoneRegex(), "TimeZoneInfo.Local"),
        (ToLocalTimeRegex(), "ToLocalTime("),
        (ConvertTimeRegex(), "TimeZoneInfo.ConvertTime"),
        (FindZoneRegex(), "FindSystemTimeZoneById"),
        (SystemZonesRegex(), "GetSystemTimeZones"),
    ];

    // Every violation in the production projects, keyed by repository path and token, with its count.
    public static IReadOnlyDictionary<(string Path, string Token), int> ScanRepository()
    {
        var found = new Dictionary<(string Path, string Token), int>();
        foreach (var file in ProductionFiles())
        {
            var path = Path.GetRelativePath(RepoPaths.Root, file).Replace('\\', '/');
            if (path == DisplayDatePath)
            {
                continue;
            }

            foreach (var token in Scan(File.ReadAllText(file)).Where(t => path != SprintLabelsPath || !IsFormatToken(t)))
            {
                found[(path, token)] = found.GetValueOrDefault((path, token)) + 1;
            }
        }

        return found;
    }

    // The tokens found in one piece of C# source, one entry per occurrence.
    public static IReadOnlyList<string> Scan(string source)
    {
        var masked = CSharpMask.Read(source);
        var tokens = new List<string>();
        foreach (var (pattern, token) in ZoneTokens)
        {
            tokens.AddRange(pattern.Matches(masked.Code).Select(_ => token));
        }

        foreach (Match call in OldOverloadRegex().Matches(masked.Code))
        {
            var name = call.Groups[1].Value;
            if (name != "Relative")
            {
                tokens.Add($"DisplayDate.{name}(");
            }
            else if (ArgumentCount(masked.Code, call.Index + call.Length - 1) == 2)
            {
                tokens.Add(OldRelative);
            }
        }

        tokens.AddRange(masked.Literals
            .Where(l => FollowsToStringCall(masked.Code, l.Start) && IsAdHocDateFormat(l.Value))
            .Select(l => FormatToken(l.Value)));
        tokens.AddRange(masked.HoleFormats
            .Where(h => IsAdHocDateFormat(h.Format) && !h.After.StartsWith(" UTC", StringComparison.Ordinal))
            .Select(h => FormatToken(h.Format)));
        return tokens.Order(StringComparer.Ordinal).ToList();
    }

    // The number of top-level arguments of the call whose '(' is at open.
    public static int ArgumentCount(string code, int open)
    {
        var depth = 0;
        var commas = 0;
        var empty = true;
        for (var i = open; i < code.Length; i++)
        {
            var c = code[i];
            depth += c switch { '(' or '[' or '{' => 1, ')' or ']' or '}' => -1, _ => 0 };
            if (depth == 0)
            {
                break;
            }

            commas += depth == 1 && c == ',' ? 1 : 0;
            empty &= i == open || char.IsWhiteSpace(c);
        }

        return empty ? 0 : commas + 1;
    }

    // A display format such as "d MMM" or "HH:mm"; ISO 8601 (with a T) and formats that name UTC are machine text.
    public static bool IsAdHocDateFormat(string format) =>
        (format.Contains("MMM", StringComparison.Ordinal) || format.Contains("ddd", StringComparison.Ordinal) || format.Contains("HH:mm", StringComparison.Ordinal))
        && !format.Contains('T', StringComparison.Ordinal)
        && !format.Contains("UTC", StringComparison.Ordinal);

    private static string FormatToken(string format) => $"format \"{format}\"";

    private static bool IsFormatToken(string token) => token.StartsWith("format ", StringComparison.Ordinal);

    // True when the literal at start is the first argument of a ToString call: `ToString(` then the literal.
    private static bool FollowsToStringCall(string code, int start)
    {
        var i = start - 1;
        while (i >= 0 && char.IsWhiteSpace(code[i]))
        {
            i--;
        }

        if (i < 0 || code[i] != '(')
        {
            return false;
        }

        var before = code[..i].TrimEnd();
        return before.EndsWith("ToString", StringComparison.Ordinal)
               && (before.Length == "ToString".Length || !IsIdentifierChar(before[^("ToString".Length + 1)]));
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static IEnumerable<string> ProductionFiles() =>
        ScannedProjects.SelectMany(project =>
        {
            var root = Path.Combine(RepoPaths.Root, "src", project);
            return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !Path.GetRelativePath(root, f).Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"));
        });

    [GeneratedRegex(@"\bLocalTimeZone\b")]
    private static partial Regex LocalTimeZoneRegex();

    [GeneratedRegex(@"\bTimeZoneInfo\s*\.\s*Local\b")]
    private static partial Regex LocalZoneRegex();

    [GeneratedRegex(@"\bToLocalTime\s*\(")]
    private static partial Regex ToLocalTimeRegex();

    [GeneratedRegex(@"\bTimeZoneInfo\s*\.\s*ConvertTime\w*")]
    private static partial Regex ConvertTimeRegex();

    [GeneratedRegex(@"\b\w*FindSystemTimeZoneById\b")]
    private static partial Regex FindZoneRegex();

    [GeneratedRegex(@"\bGetSystemTimeZones\b")]
    private static partial Regex SystemZonesRegex();

    [GeneratedRegex(@"\bDisplayDate\s*\.\s*(Relative|Clock|Weekday)\s*\(")]
    private static partial Regex OldOverloadRegex();
}
