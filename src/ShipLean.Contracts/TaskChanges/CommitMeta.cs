namespace ShipLean.Contracts.TaskChanges;

// What a commit is, without its patch. FilesChanged is Paths.Count.
public sealed record CommitMeta(string Sha, IReadOnlyList<string> ParentShas, string Subject, string AuthorName, DateTimeOffset AuthoredAt, int Additions, int Deletions, IReadOnlyList<string> Paths)
{
    public int FilesChanged => Paths.Count;
}
