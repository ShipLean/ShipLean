using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Search.Ranking;

// The tsquery of a lexical match: websearch_to_tsquery on both configs (docs/search.md). With Prefix set, the last word becomes "word:*"
// and the words before it stay an ordinary web search, ANDed. Shared by the item leg and the project and sprint groups.
public static partial class SearchTsQuery
{
    private const string Web = "(websearch_to_tsquery('english', @head) || websearch_to_tsquery('simple', @head))";

    // The any-word query reads the head with the english config only: it drops stop words ("the", "to", "in"), which the simple config
    // keeps and which would otherwise match nearly every row (about 80 ms to rank on the 498-item eval corpus, RESULTS-hybrid.md). The last word is ORed, not ANDed.
    private const string WebAny = "websearch_to_tsquery('english', @head)";

    // The SQL expression over the parameters @head and, when hasLast, @last.
    public static string Expression(bool hasLast, bool any = false)
    {
        if (any)
        {
            return hasLast ? $"({WebAny} || to_tsquery('simple', @last))" : WebAny;
        }

        return hasLast ? $"({Web} && to_tsquery('simple', @last))" : Web;
    }

    // The head with "or" between its words, for the any-word list the item leg adds when the semantic leg is unavailable (A5: 0.73 top-5
    // against ILIKE's 0.44). The prefix word (from Split) is kept only from 3 characters, so "login r" does not OR in every r* row.
    // Null when it would not differ from the AND query: fewer than two words in all, or the text uses a quote, a negation or an "or"
    // of its own, which the user meant exactly.
    public static (string Head, string? Last)? AnyWords(string head, string? last)
    {
        if (head.Contains('"') || Negation().IsMatch(head) || OrWord().IsMatch(head))
        {
            return null;
        }

        var words = Word().Matches(head).Select(m => m.Value).ToList();
        if (last is not null && last[1..^3].Replace("''", "'").Replace("\\\\", "\\").Length < MinPrefixChars)
        {
            last = null;
        }

        if (words.Count + (last is null ? 0 : 1) < 2)
        {
            return null;
        }

        return (string.Join(" or ", words), last);
    }

    private const int MinPrefixChars = 3;

    // A last word that is not plain (a quoted phrase, a -negation) or that follows an "or" (the AND would swallow it) is left to websearch_to_tsquery.
    public static (string Head, string? Last) Split(string queryText, bool prefix)
    {
        var text = queryText.TrimEnd();
        if (!prefix || text.Length != queryText.Length)
        {
            return (queryText, null);
        }

        var match = LastWord().Match(text);
        if (!match.Success || string.Equals(match.Value, "or", StringComparison.OrdinalIgnoreCase) || OrBefore().IsMatch(text[..match.Index]))
        {
            return (queryText, null);
        }

        // Quoted for to_tsquery, so the word's own punctuation (SHIP-1) cannot be read as query syntax.
        return (text[..match.Index], "'" + match.Value.Replace("\\", "\\\\").Replace("'", "''") + "':*");
    }

    // A last word made only of letters (with their marks, so Thai and Hindi words stay whole), digits, '_' and '-' that is not the
    // operand of a quote or a negation.
    [GeneratedRegex(@"(?<![\p{L}\p{M}\p{N}_\-""])[\p{L}\p{N}_][\p{L}\p{M}\p{N}_\-]*$")]
    private static partial Regex LastWord();

    [GeneratedRegex(@"(?<![\p{L}\p{M}\p{N}_\-""])or\s+$", RegexOptions.IgnoreCase)]
    private static partial Regex OrBefore();

    [GeneratedRegex(@"[\p{L}\p{N}_][\p{L}\p{M}\p{N}_\-]*")]
    private static partial Regex Word();

    [GeneratedRegex(@"(^|\s)-\S")]
    private static partial Regex Negation();

    [GeneratedRegex(@"(^|\s)or(\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex OrWord();
}
