using SearchEmbeddings.Corpus;

namespace SearchEmbeddings.Lexical;

// What the import plus the production indexer would write (docs/search.md § What goes in each field): removed items and
// archived comments produce nothing.
public static class SearchDocBuilder
{
    public static IEnumerable<SearchDocRow> Build(IReadOnlyList<CorpusFile> corpus, int chunkSize)
    {
        foreach (var item in corpus.SelectMany(f => f.Items).Where(i => i.State != "Removed"))
        {
            yield return new SearchDocRow(item.Ref, "title", 0, item.Title);
            if (item.Tags.Count > 0)
            {
                yield return new SearchDocRow(item.Ref, "tags", 0, string.Join(' ', item.Tags));
            }
            var body = string.Join("\n\n", new[] { item.Description }.Concat(item.AcceptanceCriteria).Append(item.ReproSteps).Where(s => !string.IsNullOrWhiteSpace(s)));
            foreach (var row in Rows(item, "body", body, chunkSize))
            {
                yield return row;
            }
            foreach (var comment in item.Comments.Where(c => !c.Archived && !string.IsNullOrWhiteSpace(c.Body)))
            {
                foreach (var row in Rows(item, "comment", comment.Body, chunkSize))
                {
                    yield return row;
                }
            }
        }
    }

    // chunk_no counts per (source, field); two comments of one item share chunk numbers, which the spike table allows.
    private static IEnumerable<SearchDocRow> Rows(CorpusItem item, string field, string text, int chunkSize)
        => DocChunker.Chunk(text, chunkSize).Select((c, n) => new SearchDocRow(item.Ref, field, n, item.Title + "\n" + c));
}
