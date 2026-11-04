using System.Text;
using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Demos.Brand;

// K18's design tokens, one `--name: value;` per line. Pure: no I/O. A project admin writes them and they reach an agent's browser and prompt,
// so a line is kept only when its value is a colour, a number or a font stack of allowlisted families. There is no free text, nothing that
// loads anything and nothing that can leave the rule they are written into. What is stored is the normalised lines, not what was typed.
public static partial class DemoTokens
{
    public const int MaxBytes = 8 * 1024;

    public const int MaxTokens = 100;

    public const int MaxValueLength = 200;

    public const int MaxFamilies = 8;

    private const string FontName = "--font-sans";

    private const string ColourPrefix = "--color-";

    // The seven names slides use. Any other valid name is stored and shown, and never delivered.
    public static IReadOnlyList<string> UsedNames { get; } =
        ["--color-bg", "--color-surface", "--color-text", "--color-muted", "--color-accent", "--color-accent-fg", FontName];

    // Generic families stay bare; naming a font quotes it (quoting a generic would turn it into a font called "serif").
    public static IReadOnlyList<string> FontFamilies { get; } =
    [
        "serif", "sans-serif", "monospace", "system-ui", "ui-serif", "ui-sans-serif", "ui-monospace", "-apple-system",
        "Inter", "Arial", "Helvetica", "Helvetica Neue", "Segoe UI", "Roboto", "Open Sans", "Verdana", "Tahoma", "Trebuchet MS",
        "Georgia", "Garamond", "Times New Roman", "Times", "Courier New", "Courier", "Consolas", "Menlo", "Monaco",
    ];

    private static readonly HashSet<string> Generics = new(
        ["serif", "sans-serif", "monospace", "system-ui", "ui-serif", "ui-sans-serif", "ui-monospace", "-apple-system"], StringComparer.OrdinalIgnoreCase);

    private static readonly string[] Forbidden = ["url(", "@import", "expression(", "<", ">", "{", "}", "\\"];

    public static DemoTokensResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return DemoTokensResult.Accepted(string.Empty);
        }

        if (Encoding.UTF8.GetByteCount(text) > MaxBytes)
        {
            return DemoTokensResult.Refused("Tokens can be at most 8 KiB.");
        }

        var kept = new List<string>();
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var raw = lines[index].Trim();
            if (raw.Length == 0)
            {
                continue;
            }

            var number = index + 1;
            if (kept.Count == MaxTokens)
            {
                return Refuse(number, $"there can be at most {MaxTokens} tokens.");
            }

            var normalised = ParseLine(raw, out var refusal);
            if (normalised is null)
            {
                return Refuse(number, refusal!);
            }

            kept.Add(normalised);
        }

        return DemoTokensResult.Accepted(string.Join('\n', kept));
    }

    private static DemoTokensResult Refuse(int line, string reason) => DemoTokensResult.Refused($"Line {line}: {reason}", line);

    // The normalised line, or null with the reason.
    private static string? ParseLine(string raw, out string? refusal)
    {
        refusal = null;
        if (raw.Any(char.IsControl))
        {
            refusal = "control characters aren't allowed.";
            return null;
        }

        if (raw.Contains("/*", StringComparison.Ordinal) || raw.Contains("*/", StringComparison.Ordinal))
        {
            refusal = "comments aren't allowed.";
            return null;
        }

        var banned = Forbidden.FirstOrDefault(text => raw.Contains(text, StringComparison.OrdinalIgnoreCase));
        if (banned is not null)
        {
            refusal = $"{banned} isn't allowed in a token.";
            return null;
        }

        var declaration = Declaration().Match(raw);
        if (!declaration.Success)
        {
            refusal = "write the token as --name: value;";
            return null;
        }

        var name = declaration.Groups["name"].Value;
        if (!ValidName().IsMatch(name))
        {
            refusal = name.Any(char.IsUpper)
                ? $"{name} is not a valid name: a name can't have upper-case letters."
                : $"{name} is not a valid name: it starts with -- and a letter, then lower-case letters, digits and hyphens, at most 64 characters in all.";
            return null;
        }

        var value = declaration.Groups["value"].Value.Trim();
        if (value.Length > MaxValueLength)
        {
            refusal = $"the value for {name} is over {MaxValueLength} characters.";
            return null;
        }

        var normalisedValue = NormaliseFor(name, value);
        if (normalisedValue is null)
        {
            refusal = Kind(name);
            return null;
        }

        return $"{name}: {normalisedValue};";
    }

    // A recognised name takes only its own kind; any other valid name takes a colour, a number or a font stack.
    private static string? NormaliseFor(string name, string value)
    {
        if (IsColourName(name))
        {
            return NormaliseColour(value);
        }

        return name == FontName ? NormaliseFonts(value) : NormaliseAny(value);
    }

    private static bool IsColourName(string name) => name.StartsWith(ColourPrefix, StringComparison.Ordinal) && UsedNames.Contains(name);

    private static string Kind(string name)
    {
        if (name == FontName)
        {
            return "the value for --font-sans must be font families from the allowed list, such as \"Inter\", system-ui, sans-serif.";
        }

        if (IsColourName(name))
        {
            return $"the value for {name} must be a colour: #rgb, #rrggbb, #rrggbbaa, rgb(...) or hsl(...).";
        }

        return $"the value for {name} must be a colour, a number with an optional px, rem, em or %, or font families from the allowed list.";
    }

    private static string? NormaliseAny(string value)
        => NormaliseColour(value) ?? NormaliseNumber(value) ?? NormaliseFonts(value);

    private static string? NormaliseColour(string value)
    {
        if (Hex().IsMatch(value))
        {
            return value.ToLowerInvariant();
        }

        var function = ColourFunction().Match(value);
        return function.Success && function.Groups["args"].Value.Any(char.IsDigit)
            ? $"{function.Groups["fn"].Value.ToLowerInvariant()}({function.Groups["args"].Value.Trim()})"
            : null;
    }

    private static string? NormaliseNumber(string value)
        => Number().IsMatch(value) ? value.ToLowerInvariant() : null;

    private static string? NormaliseFonts(string value)
    {
        var families = value.Split(',');
        if (families.Length > MaxFamilies)
        {
            return null;
        }

        var spelled = new List<string>();
        foreach (var family in families)
        {
            var name = Unquote(family.Trim());
            if (name is null)
            {
                return null;
            }

            var listed = FontFamilies.FirstOrDefault(known => string.Equals(known, name, StringComparison.OrdinalIgnoreCase));
            if (listed is null)
            {
                return null;
            }

            spelled.Add(Generics.Contains(listed) ? listed : $"\"{listed}\"");
        }

        return string.Join(", ", spelled);
    }

    // The family with matching quotes removed and inner runs of spaces made single, or null when it is empty or the quotes do not match.
    private static string? Unquote(string family)
    {
        if (family.Length >= 2 && family[0] is '"' or '\'' && family[^1] == family[0])
        {
            family = family[1..^1];
        }

        if (family.Contains('"') || family.Contains('\''))
        {
            return null;
        }

        family = Spaces().Replace(family.Trim(), " ");
        return family.Length == 0 ? null : family;
    }

    [GeneratedRegex(@"^(?<name>--[^:\s]*)\s*:\s*(?<value>[^;]*?)\s*;$")]
    private static partial Regex Declaration();

    [GeneratedRegex("^--[a-z][a-z0-9-]{0,63}$")]
    private static partial Regex ValidName();

    [GeneratedRegex("^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    private static partial Regex Hex();

    [GeneratedRegex(@"^(?<fn>rgba?|hsla?)\((?<args>[0-9.%,/ ]*)\)$", RegexOptions.IgnoreCase)]
    private static partial Regex ColourFunction();

    [GeneratedRegex("^[0-9]+(\\.[0-9]+)?(px|rem|em|%)?$", RegexOptions.IgnoreCase)]
    private static partial Regex Number();

    [GeneratedRegex(" {2,}")]
    private static partial Regex Spaces();
}
