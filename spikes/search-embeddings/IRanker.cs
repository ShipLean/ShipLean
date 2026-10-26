using SearchEmbeddings.Corpus;

namespace SearchEmbeddings;

// CA3. A2 and A3 add rankers by appending an entry to the dictionary in Program.cs.
public interface IRanker
{
    string Label { get; }

    Rankings Rank(IReadOnlyList<CorpusFile> corpus, IReadOnlyList<EvalQuery> queries, RankerOptions options);
}
