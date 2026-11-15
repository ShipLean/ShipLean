using Dapper;
using Npgsql;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;
using ShipLean.Core;

namespace ShipLean.Application.Features.TaskChanges.Reads.Section;

// ITaskCommitsSection over work_item_commit and code_commit. It makes no host calls: every row is Access "ok".
public sealed class TaskCommitsSection(NpgsqlDataSource dataSource, TimeProvider clock) : ITaskCommitsSection
{
    public async Task<TaskCommitsSectionDto> SectionAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT wc.id AS Id, r.provider AS Provider, r.host AS Host, r.path AS RepoPath, wc.sha AS Sha, c.subject AS Subject,
                   c.author_name AS AuthorName, c.authored_at AS AuthoredAt, COALESCE(wc.added_by_agent, u.name, '') AS AddedBy,
                   wc.added_by_agent IS NOT NULL AS AddedByAgent, c.additions AS Additions, c.deletions AS Deletions,
                   c.files_changed AS FilesChanged, c.paths AS Paths
            FROM work_item_commit wc
            JOIN code_repository r ON r.id = wc.repository_id
            JOIN code_commit c ON c.repository_id = wc.repository_id AND c.sha = wc.sha
            LEFT JOIN app_user u ON u.id = wc.added_by
            WHERE wc.work_item_id = @workItemId AND wc.archived_at IS NULL
            ORDER BY wc.position
            """,
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var openComments = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM code_comment WHERE work_item_id = @workItemId AND parent_id IS NULL AND resolved_at IS NULL AND archived_at IS NULL",
            new { workItemId }, cancellationToken: cancellationToken));

        var commits = rows.Select(Commit).ToList();
        var filePaths = rows
            .SelectMany(row => row.Paths.Select(path => new TaskChangeFilePathDto(row.RepoPath, path)))
            .Distinct()
            .ToList();
        return new TaskCommitsSectionDto(
            workItemId,
            commits,
            new TaskCommitTotalsDto(commits.Count, filePaths.Count, commits.Sum(c => c.Additions), commits.Sum(c => c.Deletions)),
            filePaths,
            openComments,
            commits.Count == 0 ? null : commits[^1].Id.ToString());
    }

    private TaskCommitRowDto Commit(Row row)
    {
        var repo = new RepoRef(row.Provider == "gitlab" ? CodeProvider.Gitlab : CodeProvider.Github, row.Host, row.RepoPath);
        return new TaskCommitRowDto(
            row.Id, repo.Name, repo.WebUrl, row.Provider, row.Sha, row.Sha[..7], row.Subject, row.AuthorName,
            DisplayDate.Relative(row.AuthoredAt, clock), row.AddedBy, row.AddedByAgent, row.Additions, row.Deletions, row.FilesChanged,
            repo.CommitUrl(row.Sha), "ok");
    }

    private sealed record Row(
        Guid Id, string Provider, string Host, string RepoPath, string Sha, string Subject, string AuthorName, DateTimeOffset AuthoredAt,
        string AddedBy, bool AddedByAgent, int Additions, int Deletions, int FilesChanged, string[] Paths);
}
