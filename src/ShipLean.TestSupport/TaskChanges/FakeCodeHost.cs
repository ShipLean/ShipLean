using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.TestSupport.TaskChanges;

// An in-memory ICodeHost over real git patches kept in TaskChanges/repos/<owner>/<name>/ (repo.json, patches/<sha>.patch,
// ranges/<base>..<head>.patch), made with git in a scratch repository. Nothing touches the network.
//   acme/storefront: a root commit (Root) and three that follow one another (First, Second, Third), plus OtherBranch,
//     a commit on another branch dated between First and Second. Missing is a sha the repository does not have.
//   acme/payments: one commit (Payments.Only).
//   acme/secret: unreadable (403). A repository with no folder is unreadable too (404).
// Register it in a test with ConfigureTestServices: services.AddFakeCodeHost() (or AddFakeCodeHost(fake) to keep a handle).
public sealed class FakeCodeHost : ICodeHost
{
    public const string Storefront = "acme/storefront";
    public const string PaymentsRepo = "acme/payments";
    public const string Secret = "acme/secret";

    public static class Shas
    {
        public const string Root = "06890a89ac17a7b73cb448c8e83013f87e59bac3";
        public const string First = "1bbb25c5575ffa9f578572ef6aa5d5ee0dffdcb9";
        public const string OtherBranch = "c2d50ebab201ec2e1ce825df4379d628cf07f86f";
        public const string Second = "7c8e48f216ae6325940ae385d3d249bd295bdad0";
        public const string Third = "510331f554511bbb25a769c621bdac8b3b5490de";
        public const string PaymentsOnly = "3129875341b21be51c23784314195d9fdb711823";
        public const string Missing = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeef";
    }

    public static RepoRef StorefrontRepo { get; } = new(CodeProvider.Github, "github.com", Storefront);

    public static RepoRef Payments { get; } = new(CodeProvider.Github, "github.com", PaymentsRepo);

    public static RepoRef SecretRepo { get; } = new(CodeProvider.Github, "github.com", Secret);

    private static readonly string ReposDirectory = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "TaskChanges", "repos");

    private static readonly Lazy<IReadOnlyDictionary<string, FakeRepo>> Repos = new(Load);

    public Task<CommitRead> ReadCommitAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
        => Task.FromResult(Read(repo, sha));

    public Task<PatchRead> ReadPatchAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
    {
        var read = Read(repo, sha);
        if (read.Status != CommitReadStatus.Found)
        {
            return Task.FromResult(new PatchRead(read.Status, null));
        }
        var file = Path.Combine(ReposDirectory, repo.Path, "patches", $"{read.Meta!.Sha}.patch");
        return Task.FromResult(new PatchRead(CommitReadStatus.Found, File.ReadAllText(file)));
    }

    public Task<PatchRead> ReadRangePatchAsync(RepoRef repo, string baseSha, string headSha, CancellationToken cancellationToken = default)
    {
        var from = Read(repo, baseSha);
        var to = Read(repo, headSha);
        if (from.Status != CommitReadStatus.Found || to.Status != CommitReadStatus.Found)
        {
            var status = from.Status == CommitReadStatus.Unreadable || to.Status == CommitReadStatus.Unreadable ? CommitReadStatus.Unreadable : CommitReadStatus.Missing;
            return Task.FromResult(new PatchRead(status, null));
        }
        var file = Path.Combine(ReposDirectory, repo.Path, "ranges", $"{from.Meta!.Sha}..{to.Meta!.Sha}.patch");
        return Task.FromResult(File.Exists(file)
            ? new PatchRead(CommitReadStatus.Found, File.ReadAllText(file))
            : new PatchRead(CommitReadStatus.Missing, null));
    }

    // The meta of a commit the fake has, for a test that arranges rows (CodeChangesSeed). Throws when there is none.
    public static CommitMeta MetaOf(string repoPath, string sha)
    {
        var read = Read(new RepoRef(CodeProvider.Github, "github.com", repoPath), sha);
        return read.Meta ?? throw new InvalidOperationException($"The fake has no commit {sha} in {repoPath} ({read.Status}).");
    }

    private static CommitRead Read(RepoRef repo, string sha)
    {
        if (!Repos.Value.TryGetValue(repo.Path, out var fake))
        {
            return new CommitRead(CommitReadStatus.Unreadable, null, 404);
        }
        if (!fake.Readable)
        {
            return new CommitRead(CommitReadStatus.Unreadable, null, 403);
        }
        var matches = sha.Length >= 7 ? fake.Commits.Where(c => c.Sha.StartsWith(sha, StringComparison.OrdinalIgnoreCase)).ToList() : [];
        return matches.Count == 1
            ? new CommitRead(CommitReadStatus.Found, matches[0], null)
            : new CommitRead(CommitReadStatus.Missing, null, 404);
    }

    private static Dictionary<string, FakeRepo> Load()
    {
        var repos = new Dictionary<string, FakeRepo>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(ReposDirectory, "repo.json", SearchOption.AllDirectories))
        {
            var json = JsonSerializer.Deserialize<RepoFile>(File.ReadAllText(file), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var path = Path.GetRelativePath(ReposDirectory, Path.GetDirectoryName(file)!).Replace('\\', '/');
            var commits = json.Commits.Select(c => new CommitMeta(c.Sha, c.Parents, c.Subject, c.Author, DateTimeOffset.Parse(c.AuthoredAt, System.Globalization.CultureInfo.InvariantCulture), c.Additions, c.Deletions, c.Paths)).ToList();
            repos[path] = new FakeRepo(json.Readable, commits);
        }
        return repos;
    }

    private sealed record FakeRepo(bool Readable, IReadOnlyList<CommitMeta> Commits);

    private sealed record RepoFile(bool Readable, List<CommitFile> Commits);

    private sealed record CommitFile(string Sha, List<string> Parents, string Subject, string Author, string AuthoredAt, int Additions, int Deletions, List<string> Paths);
}

public static class FakeCodeHostServices
{
    // Replaces the app's ICodeHost with the fake.
    public static IServiceCollection AddFakeCodeHost(this IServiceCollection services) => services.AddFakeCodeHost(new FakeCodeHost());

    public static IServiceCollection AddFakeCodeHost(this IServiceCollection services, FakeCodeHost fake)
    {
        services.RemoveAll<ICodeHost>();
        return services.AddSingleton<ICodeHost>(fake);
    }
}
