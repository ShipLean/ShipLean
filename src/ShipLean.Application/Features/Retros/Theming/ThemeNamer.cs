using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Retros.Theming;

// Names an auto theme from its first point (D9): the first sentence without trailing punctuation, at most 6 words and 48
// characters. A name another live theme already uses gets " (2)", " (3)" and so on, kept inside the 48 characters.
public static partial class ThemeNamer
{
    public const int MaxWords = 6;
    public const int MaxLength = 48;
    private const string Fallback = "Untitled theme";
    private const string TrailingPunctuation = ".,;:!?-–—…\"')]} ";

    public static string Name(string text, IReadOnlyCollection<string> existingNames)
    {
        var baseName = Trim(text);
        var taken = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(baseName))
        {
            return baseName;
        }

        var number = 2;
        while (true)
        {
            var suffix = $" ({number++})";
            var candidate = Trim(baseName, MaxLength - suffix.Length) + suffix;
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static string Trim(string text) => Trim(FirstSentence(text), MaxLength);

    private static string Trim(string sentence, int maxLength)
    {
        var words = sentence.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(MaxWords);
        var name = string.Join(' ', words);
        if (name.Length > maxLength)
        {
            var cut = name.LastIndexOf(' ', maxLength);
            name = cut > 0 ? name[..cut] : name[..maxLength];
        }

        name = name.TrimEnd(TrailingPunctuation.ToCharArray());
        return name.Length == 0 ? Fallback : name;
    }

    private static string FirstSentence(string text)
    {
        var trimmed = text.Trim();
        var end = SentenceEnd().Match(trimmed);
        return end.Success ? trimmed[..end.Index] : trimmed;
    }

    // A full stop, question or exclamation mark followed by white space, or a line break.
    [GeneratedRegex(@"[.!?]+(?=\s)|\r?\n")]
    private static partial Regex SentenceEnd();
}
