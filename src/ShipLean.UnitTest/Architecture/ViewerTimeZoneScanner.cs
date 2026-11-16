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

    public const string UsingStatic = "using static TimeZoneInfo";
    public const string UsingAlias = "using alias TimeZoneInfo";

    private static readonly (Regex Pattern, string Token)[] ZoneTokens =
    [
        (LocalTimeZoneRegex(), "LocalTimeZone"),
        (ToLocalTimeRegex(), "ToLocalTime("),
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
            foreach (var token in ScanFile(path, File.ReadAllText(file)))
            {
                found[(path, token)] = found.GetValueOrDefault((path, token)) + 1;
            }
        }

        return found;
    }

    // The tokens found in one production file (path from the repository root), after the permanent exceptions.
    public static IReadOnlyList<string> ScanFile(string path, string source) =>
        path == DisplayDatePath
            ? []
            : Scan(source);

    // The tokens found in one piece of C# source, one entry per occurrence.
    public static IReadOnlyList<string> Scan(string source)
    {
        var masked = CSharpMask.Read(source);
        var tokens = new List<string>();
        foreach (var (pattern, token) in ZoneTokens)
        {
            tokens.AddRange(pattern.Matches(masked.Code).Select(_ => token));
        }

        tokens.AddRange(ZoneTypeTokens(masked.Code));

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
    public static int ArgumentCount(string code, int open) => Arguments(code, open).Count;

    // The top-level arguments of the call whose '(' is at open, trimmed.
    private static List<string> Arguments(string code, int open)
    {
        var depth = 0;
        var start = open + 1;
        var arguments = new List<string>();
        for (var i = open; i < code.Length; i++)
        {
            var c = code[i];
            depth += c switch { '(' or '[' or '{' => 1, ')' or ']' or '}' => -1, _ => 0 };
            if (depth == 0 || (depth == 1 && c == ','))
            {
                arguments.Add(code[start..i].Trim());
                start = i + 1;
            }

            if (depth == 0)
            {
                break;
            }
        }

        return arguments is [""] ? [] : arguments;
    }

    // TimeZoneInfo.Local and TimeZoneInfo.ConvertTime*, also through `using static System.TimeZoneInfo` (bare Local,
    // ConvertTime) and `using Z = System.TimeZoneInfo` (Z.Local, Z.ConvertTime); the usings are flagged themselves. A
    // ConvertTime whose zone arguments are all literally TimeZoneInfo.Utc is not zone code.
    private static IEnumerable<string> ZoneTypeTokens(string code)
    {
        var tokens = new List<string>();
        var isStatic = UsingStaticRegex().IsMatch(code);
        tokens.AddRange(UsingStaticRegex().Matches(code).Select(_ => UsingStatic));
        var aliases = UsingAliasRegex().Matches(code).Select(m => m.Groups[1].Value).ToList();
        tokens.AddRange(aliases.Select(_ => UsingAlias));

        var qualifiers = string.Join("|", aliases.Select(Regex.Escape).Prepend("TimeZoneInfo"));
        var qualified = $@"\b(?:{qualifiers})\s*\.\s*";
        var bare = isStatic ? @"|(?<![\w.])(?<!\.\s+)" : "";
        var utc = isStatic ? $@"(?:(?:{qualifiers})\s*\.\s*)?Utc" : $@"(?:{qualifiers})\s*\.\s*Utc";

        tokens.AddRange(Regex.Matches(code, $@"(?:{qualified}{bare})Local\b").Select(_ => "TimeZoneInfo.Local"));
        foreach (Match convert in Regex.Matches(code, $@"(?:{qualified}{bare})(ConvertTime\w*)"))
        {
            var open = convert.Index + convert.Length;
            while (open < code.Length && char.IsWhiteSpace(code[open]))
            {
                open++;
            }

            var toUtc = convert.Groups[1].Value == "ConvertTime" && open < code.Length && code[open] == '('
                        && Arguments(code, open) is [_, _, ..] arguments
                        && arguments.Skip(1).All(a => Regex.IsMatch(a, $"^{utc}$"));
            if (!toUtc)
            {
                tokens.Add("TimeZoneInfo.ConvertTime");
            }
        }

        return tokens;
    }

    // A display format such as "d MMM" or "HH:mm"; ISO 8601 (with a T) and formats that name UTC are machine text.
    public static bool IsAdHocDateFormat(string format) =>
        (format.Contains("MMM", StringComparison.Ordinal) || format.Contains("ddd", StringComparison.Ordinal) || format.Contains("HH:mm", StringComparison.Ordinal))
        && !format.Contains('T', StringComparison.Ordinal)
        && !format.Contains("UTC", StringComparison.Ordinal);

    private static string FormatToken(string format) => $"format \"{format}\"";

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

    [GeneratedRegex(@"\bToLocalTime\s*\(")]
    private static partial Regex ToLocalTimeRegex();

    [GeneratedRegex(@"\busing\s+static\s+(?:global::)?(?:System\s*\.\s*)?TimeZoneInfo\s*;")]
    private static partial Regex UsingStaticRegex();

    [GeneratedRegex(@"\busing\s+(\w+)\s*=\s*(?:global::)?(?:System\s*\.\s*)?TimeZoneInfo\s*;")]
    private static partial Regex UsingAliasRegex();

    [GeneratedRegex(@"\b\w*FindSystemTimeZoneById\b")]
    private static partial Regex FindZoneRegex();

    [GeneratedRegex(@"\bGetSystemTimeZones\b")]
    private static partial Regex SystemZonesRegex();

    [GeneratedRegex(@"\bDisplayDate\s*\.\s*(Relative|Clock|Weekday)\s*\(")]
    private static partial Regex OldOverloadRegex();
}
