using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.ShippedIn;

// The verified facts behind a "shipped in" message (D14). Keys an agent names are only leads: each is looked up among the
// project's own items, and only that item's merged github.com pull request links count, with the merge commit and merge time
// ShipLean stored (link_state.merge_commit_sha, merged_at). Each commit's image publication comes from IImagePublications.
// An agent's commit, publishedAt or runUrl is never read. SHIP-758 (already shipped) and SHIP-763 (fixed in) share it.
public sealed partial class ShippedInFacts(NpgsqlDataSource dataSource, IImagePublications publications)
{
    public const int MaxKeys = IssueProposal.MaxExisting;

    private sealed record PullRow(string Key, string UrlKey, string MergeSha, DateTimeOffset MergedAt);

    public async Task<IReadOnlyList<ShippedFact>> FindAsync(Guid projectId, IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        var wanted = keys.Select(k => k.Trim().ToUpperInvariant()).Where(k => ItemKey().IsMatch(k)).Distinct(StringComparer.Ordinal).Take(MaxKeys).ToArray();
        if (wanted.Length == 0)
        {
            return [];
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PullRow>(new CommandDefinition(
            """
            SELECT w.key, link_url_key(l.url) AS urlKey, ls.merge_commit_sha AS mergeSha, ls.merged_at AS mergedAt
            FROM work_item w
            JOIN work_item_link l ON l.source_id = w.id AND l.link_type = 'url' AND l.archived_at IS NULL
            JOIN link_state ls ON ls.url_key = link_url_key(l.url)
            WHERE w.project_id = @projectId AND w.key = ANY(@wanted)
              AND link_url_key(l.url) ~ '^github\.com/[^/]+/[^/]+/pull/[0-9]+$'
              AND ls.state = 'merged' AND ls.merge_commit_sha IS NOT NULL AND ls.merged_at IS NOT NULL
            ORDER BY ls.merged_at, w.key, link_url_key(l.url)
            """,
            new { projectId, wanted },
            cancellationToken: cancellationToken));

        var facts = new List<ShippedFact>();
        foreach (var row in rows)
        {
            var pull = PullKey().Match(row.UrlKey);
            var sha = row.MergeSha.Trim().ToLowerInvariant();
            if (!pull.Success || !Sha().IsMatch(sha) || facts.Exists(f => f.MergeCommitSha == sha && f.Key == row.Key))
            {
                continue;
            }

            var repository = $"{pull.Groups["owner"].Value}/{pull.Groups["repo"].Value}";
            var publication = await publications.FindAsync(new IssueRef(IssueRef.GitHubHost, repository, 0), sha, cancellationToken);
            facts.Add(new ShippedFact(
                row.Key, repository, int.Parse(pull.Groups["number"].Value, CultureInfo.InvariantCulture), sha, row.MergedAt.ToUniversalTime(), publication));
        }

        return facts;
    }

    [GeneratedRegex(@"^[A-Z][A-Z0-9]{0,9}-[0-9]{1,9}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ItemKey();

    [GeneratedRegex(@"^github\.com/(?<owner>[A-Za-z0-9-]+)/(?<repo>[A-Za-z0-9._-]+)/pull/(?<number>[0-9]{1,9})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PullKey();

    [GeneratedRegex("^[0-9a-f]{7,40}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sha();
}
