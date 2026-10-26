using System.Text;
using SearchEmbeddings.Corpus;

namespace SearchEmbeddings.Embedding;

public enum ChunkField
{
    Title,
    Body,
    Comment,
}

public sealed record Chunk(string ItemRef, ChunkField Field, string Text);

// docs/search.md: the title is its own chunk; the body (description, acceptance criteria, repro steps, tags) and every comment are
// split at about chunkSize characters on paragraph boundaries with a short overlap, and each body or comment chunk starts with the
// item's title. Removed items and archived comments are left out.
public static class Chunker
{
    private const int Overlap = 120;

    public static IEnumerable<Chunk> ChunksOf(CorpusItem item, int chunkSize)
    {
        yield return new Chunk(item.Ref, ChunkField.Title, item.Title);
        foreach (var part in Split(Body(item), chunkSize))
        {
            yield return new Chunk(item.Ref, ChunkField.Body, item.Title + "\n" + part);
        }
        foreach (var comment in item.Comments.Where(c => !c.Archived))
        {
            foreach (var part in Split(comment.Body, chunkSize))
            {
                yield return new Chunk(item.Ref, ChunkField.Comment, item.Title + "\n" + part);
            }
        }
    }

    private static string Body(CorpusItem item)
    {
        var sb = new StringBuilder(item.Description);
        if (item.AcceptanceCriteria.Count > 0)
        {
            sb.Append("\n\nAcceptance criteria:\n").AppendJoin('\n', item.AcceptanceCriteria.Select(c => "- " + c));
        }
        if (!string.IsNullOrWhiteSpace(item.ReproSteps))
        {
            sb.Append("\n\nRepro steps:\n").Append(item.ReproSteps);
        }
        if (item.Tags.Count > 0)
        {
            sb.Append("\n\nTags: ").AppendJoin(", ", item.Tags);
        }
        return sb.ToString().Trim();
    }

    private static IEnumerable<string> Split(string text, int size)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }
        var current = new StringBuilder();
        foreach (var paragraph in Paragraphs(text, size))
        {
            if (current.Length > 0 && current.Length + paragraph.Length + 2 > size)
            {
                var done = current.ToString();
                yield return done;
                current.Clear().Append(Tail(done));
            }
            if (current.Length > 0)
            {
                current.Append("\n\n");
            }
            current.Append(paragraph);
        }
        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    // Paragraphs, with any longer than size cut at the last whitespace before the limit.
    private static IEnumerable<string> Paragraphs(string text, int size)
    {
        foreach (var raw in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var rest = raw;
            while (rest.Length > size)
            {
                var cut = rest.LastIndexOfAny([' ', '\n'], size - 1);
                if (cut <= 0)
                {
                    cut = size;
                }
                yield return rest[..cut].Trim();
                rest = rest[cut..].Trim();
            }
            if (rest.Length > 0)
            {
                yield return rest;
            }
        }
    }

    // The last Overlap characters from a word boundary, so the next chunk opens with context.
    private static string Tail(string text)
    {
        if (text.Length <= Overlap)
        {
            return "";
        }
        var from = text.IndexOf(' ', text.Length - Overlap);
        return from < 0 ? "" : text[(from + 1)..];
    }
}
