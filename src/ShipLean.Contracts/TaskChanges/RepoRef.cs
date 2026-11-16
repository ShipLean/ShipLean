namespace ShipLean.Contracts.TaskChanges;

// A repository on a GitHub or GitLab host. Host and Path are lower case with no .git (RepoAddresses makes them so).
public sealed record RepoRef(CodeProvider Provider, string Host, string Path)
{
    // acme/storefront, the way a person writes it.
    public string Name => Path;

    public string WebUrl => $"https://{Host}/{Path}";

    // GitHub: /o/r/commit/sha. GitLab: /o/r/-/commit/sha.
    public string CommitUrl(string sha) => Provider == CodeProvider.Gitlab ? $"{WebUrl}/-/commit/{sha}" : $"{WebUrl}/commit/{sha}";
}
