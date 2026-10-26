using System.Text;

namespace SearchEmbeddings.Lexical;

// docs/search.md § Chunking: split at about `limit` characters on paragraph boundaries, repeating `overlap` characters at the
// start of the next chunk; a paragraph longer than the limit is cut at the limit.
public static class DocChunker
{
    public static IReadOnlyList<string> Chunk(string text, int limit, int overlap = 150)
    {
        var chunks = new List<string>();
        var current = new StringBuilder();
        var fresh = true; // current holds nothing but the overlap carried from the previous chunk
        void Flush()
        {
            if (fresh)
            {
                return;
            }
            var done = current.ToString();
            chunks.Add(done);
            current.Clear().Append(done.Length > overlap ? done[^overlap..] : done);
            fresh = true;
        }

        foreach (var paragraph in text.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            var rest = paragraph;
            while (rest.Length > 0)
            {
                var sep = current.Length == 0 ? 0 : 2;
                if (!fresh && current.Length + sep + rest.Length > limit)
                {
                    Flush();
                    sep = current.Length == 0 ? 0 : 2;
                }
                var room = Math.Max(1, limit - current.Length - sep);
                var take = rest.Length <= room ? rest : rest[..room];
                if (sep > 0)
                {
                    current.Append("\n\n");
                }
                current.Append(take);
                fresh = false;
                rest = rest[take.Length..];
                if (rest.Length > 0)
                {
                    Flush();
                }
            }
        }
        if (!fresh)
        {
            chunks.Add(current.ToString());
        }
        return chunks;
    }
}
