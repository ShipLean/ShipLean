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
            foreach (var part in Split(word))
            {
                wanted.Add(part);
            }
        }

        if (wanted.Count == 0)
        {
            return 0;
        }

        var shared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in Split(title).Where(wanted.Contains))
        {
            shared.Add(part);
        }

        return shared.Count;
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
