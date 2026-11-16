using Dapper;
using Npgsql;
using ShipLean.Application.Features.TaskChanges.Comments;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Features.TaskChanges.Reads.Page;

// ITaskChangesPage: the item, its live commits, the selected diff parts and the code comments.
// "all": per repository, commits that follow one another (each one's first parent is the one before; the first is not a root commit)
// become one combined part; any other repository gets a part per commit. A commit is chosen by its work_item_commit id, never by sha.
public sealed class TaskChangesPage(
    NpgsqlDataSource dataSource,
    IWorkItemStore store,
    IStateCatalog catalog,
    ICodeHost host,
    ICodeComments codeComments,
    TimeProvider clock,
    IViewerZone viewerZone) : ITaskChangesPage
{
    private const string All = "all";

    public async Task<TaskChangesDto> PageAsync(Guid workItemId, string commit, CancellationToken cancellationToken = default)
    {
        var item = await store.GetAsync(workItemId, cancellationToken);
        if (item.Type is not (WorkItemTypeId.Task or WorkItemTypeId.Bug))
        {
            throw ApiException.NotTaskOrBug($"{item.Key} is a {item.Type}; only Tasks and Bugs have changes.");
        }

        var states = await catalog.ListAsync(item.ProjectId, cancellationToken);
        var category = states.First(s => s.Id == item.StateId).Category.ToString();
        var rows = await ReadRowsAsync(workItemId, cancellationToken);
        var single = !(string.IsNullOrWhiteSpace(commit) || commit == All);
        var chosen = Choose(rows, commit, single);

        var access = new Dictionary<Guid, string>();
        var parts = new List<(DiffPartDto Part, bool Combined)>();
        foreach (var group in chosen.GroupBy(r => r.Repo))
        {
            parts.AddRange(await PartsOfAsync(group.Key, [.. group], single, access, cancellationToken));
        }

        var comments = await codeComments.ListAsync(workItemId, cancellationToken);
        var open = comments.Where(c => c.ResolvedAt is null).ToList();
        var shown = parts.Select(p => p.Part with { Files = [.. p.Part.Files.Select(f => f with { OpenComments = open.Count(c => c.Repo == p.Part.Repo && c.Path == f.Path) })] }).ToList();
        var mode = parts.Count == 1 && parts[0].Combined ? "combined" : "perCommit";
        if (single)
        {
            mode = "single";
        }


        return new TaskChangesDto(
            workItemId, item.Key, item.Title, item.Type == WorkItemTypeId.Bug ? "bug" : "task", char.ToLowerInvariant(category[0]) + category[1..],
            [.. rows.Select(r => Dto(r, access.GetValueOrDefault(r.Id, "ok")))], single ? chosen[0].Id.ToString() : All,
            mode, rows.Count == 0 ? null : rows[^1].Id.ToString(), shown,
            [.. comments.Select(c => CodeCommentDtos.From(c, clock.GetUtcNow(), viewerZone.Zone))], open.Count);
    }

    private static List<Row> Choose(List<Row> rows, string commit, bool single)
    {
        if (!single)
        {
            return rows;
        }

        var found = Guid.TryParse(commit, out var id) ? rows.FirstOrDefault(r => r.Id == id) : null;
        return [found ?? throw ApiException.NotFound("That commit is not one of this item's commits.", "commit")];
    }

    private async Task<List<(DiffPartDto Part, bool Combined)>> PartsOfAsync(
        RepoRef repo, List<Row> commits, bool single, Dictionary<Guid, string> access, CancellationToken cancellationToken)
    {
        if (!single && Chains(commits))
        {
            var range = await host.ReadRangePatchAsync(repo, commits[0].Parents[0], commits[^1].Sha, cancellationToken);
            if (range is { Status: CommitReadStatus.Found, UnifiedDiff: { } diff })
            {
                return [(new DiffPartDto(repo.Name, null, commits[0].Parents[0], commits[^1].Sha, GitDiffSplitter.Split(diff)), true)];
            }
        }

        var parts = new List<(DiffPartDto, bool)>();
        foreach (var row in commits)
        {
            var read = await host.ReadPatchAsync(repo, row.Sha, cancellationToken);
            if (read is { Status: CommitReadStatus.Found, UnifiedDiff: { } diff })
            {
                parts.Add((new DiffPartDto(repo.Name, row.Sha, null, row.Sha, GitDiffSplitter.Split(diff)), false));
            }
            else
            {
                access[row.Id] = read.Status == CommitReadStatus.Missing ? "missing" : "unreadable";
            }
        }

        return parts;
    }

    private static bool Chains(List<Row> commits)
    {
        if (commits[0].Parents.Length == 0)
        {
            return false;
        }

        for (var i = 1; i < commits.Count; i++)
        {
            if (commits[i].Parents.FirstOrDefault() != commits[i - 1].Sha)
            {
                return false;
            }
        }

        return true;
    }

    private TaskCommitRowDto Dto(Row r, string access)
    {
        var repo = r.Repo;
        return new TaskCommitRowDto(
            r.Id, repo.Name, repo.WebUrl, r.Provider, r.Sha, r.Sha[..7], r.Subject, r.AuthorName, DisplayDate.Relative(r.AuthoredAt, clock.GetUtcNow(), viewerZone.Zone),
            r.AddedBy, r.AddedByAgent, r.Additions, r.Deletions, r.FilesChanged, repo.CommitUrl(r.Sha), access);
    }

    private async Task<List<Row>> ReadRowsAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<RawRow>(new CommandDefinition(
            """
            SELECT w.id, w.sha, r.provider, r.host, r.path, c.parent_shas AS ParentShas, c.subject, c.author_name AS AuthorName,
                   c.authored_at AS AuthoredAt, c.additions, c.deletions, c.files_changed AS FilesChanged,
                   COALESCE(w.added_by_agent, u.name, '') AS AddedBy, w.added_by_agent IS NOT NULL AS AddedByAgent
            FROM work_item_commit w
            JOIN code_repository r ON r.id = w.repository_id
            JOIN code_commit c ON c.repository_id = w.repository_id AND c.sha = w.sha
            LEFT JOIN app_user u ON u.id = w.added_by
            WHERE w.work_item_id = @workItemId AND w.archived_at IS NULL
            ORDER BY w.position, w.id
            """, new { workItemId }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => new Row(
            r.Id, new RepoRef(r.Provider == "gitlab" ? CodeProvider.Gitlab : CodeProvider.Github, r.Host, r.Path), r.Provider, r.Sha, [.. r.ParentShas.Cast<string>()], r.Subject,
            r.AuthorName, r.AuthoredAt, r.Additions, r.Deletions, r.FilesChanged, r.AddedBy, r.AddedByAgent))];
    }

    // Dapper matches the constructor by exact column types: a text[] column arrives as Array.
    private sealed record RawRow(
        Guid Id, string Sha, string Provider, string Host, string Path, Array ParentShas, string Subject, string AuthorName,
        DateTimeOffset AuthoredAt, int Additions, int Deletions, int FilesChanged, string AddedBy, bool AddedByAgent);

    private sealed record Row(
        Guid Id, RepoRef Repo, string Provider, string Sha, string[] Parents, string Subject, string AuthorName,
        DateTimeOffset AuthoredAt, int Additions, int Deletions, int FilesChanged, string AddedBy, bool AddedByAgent);
}
