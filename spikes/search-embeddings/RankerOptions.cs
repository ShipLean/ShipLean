namespace SearchEmbeddings;

// Every flag Program.cs parses. A ranker reads the ones it needs and ignores the rest.
public sealed record RankerOptions(
    int ChunkSize,
    string? ModelDir,
    IReadOnlyList<string> Inputs,
    string? Cutoffs);
