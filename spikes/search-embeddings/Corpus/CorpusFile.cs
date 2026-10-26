namespace SearchEmbeddings.Corpus;

// One project of the eval corpus (CA1), as scripts/search-eval/corpus/<project>.json.
public sealed record CorpusFile(CorpusProject Project, IReadOnlyList<CorpusSprint> Sprints, IReadOnlyList<CorpusItem> Items);
