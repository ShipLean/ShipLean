namespace SearchEmbeddings.Corpus;

// One labelled query (CA2); Kind is exactKey, titleWord, paraphrase, bodyOnly, commentOnly, prefix or nonEnglish.
public sealed record EvalQuery(string Id, string Text, string Kind, IReadOnlyList<string> Expected);
