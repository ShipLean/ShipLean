using System.Text;

namespace ShipLean.Core;

// How many distinct words a title shares with a word list, for search ranking and likely-duplicate detection. Words are
// lower-cased runs of letters and digits; a small fixed list of English stop words never counts.
public static class WordOverlap
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "but", "by", "for", "from", "has", "in", "into", "is", "it", "its",
        "of", "on", "or", "that", "the", "their", "then", "there", "these", "this", "to", "was", "when", "will", "with",
    };

    public static int Score(string title, IReadOnlyCollection<string> words)
    {
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in words)
        {
            wanted.UnionWith(Split(word));
        }

        return Shared(Words(title), wanted);
    }

    // The distinct words of a title, prepared once for Shared.
    public static HashSet<string> Words(string title) => new(Split(title), StringComparer.Ordinal);

    // How many words two prepared titles share; Score(a, [b]) is Shared(Words(a), Words(b)).
    public static int Shared(HashSet<string> first, HashSet<string> second)
    {
        var (small, large) = first.Count <= second.Count ? (first, second) : (second, first);
        var shared = 0;
        foreach (var word in small)
        {
            if (large.Contains(word))
            {
                shared++;
            }
        }

        return shared;
    }

    // NFC-normalised, lower-cased words of the text; stop words and single letters (not digits) dropped.
    private static IEnumerable<string> Split(string raw)
    {
        var text = raw.Normalize(NormalizationForm.FormC);
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var inWord = i < text.Length && char.IsLetterOrDigit(text[i]);
            if (inWord && start < 0)
            {
                start = i;
            }
            else if (!inWord && start >= 0)
            {
                var word = text.AsSpan(start, i - start).ToString().ToLowerInvariant();
                start = -1;
                if ((word.Length > 1 || char.IsDigit(word[0])) && !StopWords.Contains(word))
                {
                    yield return word;
                }
            }
        }
    }
}
