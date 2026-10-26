using System.Text;
using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Search.Index.Chunking;

// Splits a field's text into chunks of about maxChars on paragraph boundaries (docs/search.md). A paragraph over the limit is
// split on sentences, and a sentence over the limit is cut at the limit. Each chunk after the first starts with the last
// overlap characters of the one before, so a match across a boundary is in one chunk. The limit counts the text of the field,
// so a chunk can run over by the overlap and the prefix; that is the "about". The prefix (the item's title and a newline) is
// added to every chunk and counts for nothing.
public static partial class TextChunker
{
    private const string Joiner = "\n\n";

    public static IReadOnlyList<string> Chunk(string text, string? prefix, int maxChars, int overlap)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChars);
        ArgumentOutOfRangeException.ThrowIfNegative(overlap);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(overlap, maxChars);

        var units = Units(text, maxChars).ToList();
        var chunks = new List<string>();
        var current = new StringBuilder();
        var hasNew = false;
        foreach (var unit in units)
        {
            if (hasNew && current.Length + Joiner.Length + unit.Length > maxChars)
            {
                chunks.Add(current.ToString());
                var tail = Tail(chunks[^1], overlap);
                current.Clear().Append(tail);
                hasNew = false;
            }

            if (current.Length > 0)
            {
                current.Append(Joiner);
            }
            current.Append(unit);
            hasNew = true;
        }

        if (hasNew)
        {
            chunks.Add(current.ToString());
        }

        return prefix is { Length: > 0 } ? [.. chunks.Select(c => $"{prefix}\n{c}")] : chunks;
    }

    // Paragraphs, with the long ones broken into sentences and the longest sentences cut.
    private static IEnumerable<string> Units(string text, int maxChars)
    {
        foreach (var paragraph in ParagraphBreak().Split(text.Replace("\r\n", "\n")).Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            if (paragraph.Length <= maxChars)
            {
                yield return paragraph;
                continue;
            }

            var pending = new StringBuilder();
            foreach (var sentence in SentenceBreak().Split(paragraph).SelectMany(s => Cut(s, maxChars)))
            {
                if (pending.Length > 0 && pending.Length + 1 + sentence.Length > maxChars)
                {
                    yield return pending.ToString();
                    pending.Clear();
                }
                pending.Append(pending.Length > 0 ? " " : string.Empty).Append(sentence);
            }

            if (pending.Length > 0)
            {
                yield return pending.ToString();
            }
        }
    }

    private static IEnumerable<string> Cut(string text, int maxChars)
    {
        for (var start = 0; start < text.Length;)
        {
            var end = Math.Min(start + maxChars, text.Length);
            // Never cut between the halves of a surrogate pair.
            if (end < text.Length && end - 1 > start && char.IsLowSurrogate(text[end]))
            {
                end--;
            }
            yield return text[start..end];
            start = end;
        }
    }

    // The last overlap characters of a chunk, moved forward to a word start so no word begins mid-way.
    private static string Tail(string chunk, int overlap)
    {
        if (overlap == 0 || chunk.Length <= overlap)
        {
            return overlap == 0 ? string.Empty : chunk;
        }

        var start = chunk.Length - overlap;
        if (char.IsLowSurrogate(chunk[start]))
        {
            start++;
        }
        else if (!char.IsWhiteSpace(chunk[start - 1]))
        {
            var space = chunk.IndexOf(' ', start);
            start = space < 0 ? start : space + 1;
        }
        return chunk[start..].TrimStart();
    }

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex ParagraphBreak();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceBreak();
}
