namespace SearchEmbeddings.Corpus;

public sealed record CorpusItem(
    string Ref,
    string Type,
    string Title,
    string Description,
    IReadOnlyList<string> AcceptanceCriteria,
    string ReproSteps,
    IReadOnlyList<string> Tags,
    string State,
    string? ParentRef,
    string? SprintRef,
    IReadOnlyList<CorpusComment> Comments);
