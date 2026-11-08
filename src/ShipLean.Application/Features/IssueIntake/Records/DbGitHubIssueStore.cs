using System.Data.Common;
using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Records;

// github_issue over Dapper. A method given a transaction runs on its connection; otherwise it opens its own.
public sealed class DbGitHubIssueStore(NpgsqlDataSource dataSource, TimeProvider clock) : IGitHubIssueStore
{
    private const string Columns =
        """
        id, host, repository, number, project_id AS projectId, work_item_id AS workItemId, author_login AS authorLogin, phase,
        proposal::text AS proposal, proposal_session_id AS proposalSessionId, rounds, open_on_github AS openOnGitHub,
        last_posted::text AS lastPosted, created_at AS createdAt, updated_at AS updatedAt
        """;

    private sealed record Row(
        Guid Id, string Host, string Repository, int Number, Guid ProjectId, Guid WorkItemId, string AuthorLogin, string Phase, string? Proposal,
        Guid? ProposalSessionId, int Rounds, bool OpenOnGitHub, string LastPosted, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
    {
        public GitHubIssueRecord ToRecord() => new(
            Id, new IssueRef(Host, Repository, Number), ProjectId, WorkItemId, AuthorLogin, IssueWire.ParsePhase(Phase), Proposal, ProposalSessionId,
            Rounds, OpenOnGitHub, JsonSerializer.Deserialize<Dictionary<string, string>>(LastPosted) ?? [], CreatedAt, UpdatedAt);
    }

    public Task<GitHubIssueRecord?> FindAsync(IssueRef issue, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => QueryOneAsync(
            $"SELECT {Columns} FROM github_issue WHERE host = @Host AND repository = @Repository AND number = @Number",
            new { issue.Host, issue.Repository, issue.Number }, transaction, cancellationToken);

    public Task<GitHubIssueRecord?> FindByIdAsync(Guid id, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => QueryOneAsync($"SELECT {Columns} FROM github_issue WHERE id = @id", new { id }, transaction, cancellationToken);

    public Task<GitHubIssueRecord?> FindByWorkItemAsync(Guid workItemId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => QueryOneAsync(
            $"SELECT {Columns} FROM github_issue WHERE work_item_id = @workItemId ORDER BY created_at LIMIT 1", new { workItemId }, transaction, cancellationToken);

    public async Task<GitHubIssueRecord> InsertAsync(
        IssueRef issue, Guid projectId, Guid workItemId, string authorLogin, IssuePhase phase, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        // ON CONFLICT DO NOTHING waits for a concurrent insert of the same issue to commit, so the read after it sees that row.
        const string Insert =
            """
            INSERT INTO github_issue (id, host, repository, number, project_id, work_item_id, author_login, phase, created_at, updated_at)
            VALUES (@id, @Host, @Repository, @Number, @projectId, @workItemId, @authorLogin, @phase, @now, @now)
            ON CONFLICT (host, repository, number) DO NOTHING
            """;
        await dataSource.RunAsync(
            (connection, tx) => connection.ExecuteAsync(new CommandDefinition(
                Insert,
                new { id = Guid.CreateVersion7(), issue.Host, issue.Repository, issue.Number, projectId, workItemId, authorLogin, phase = IssueWire.Phase(phase), now = clock.GetUtcNow() },
                tx,
                cancellationToken: cancellationToken)),
            transaction,
            cancellationToken);
        return await FindAsync(issue, transaction, cancellationToken)
               ?? throw new InvalidOperationException($"github_issue {issue} was neither inserted nor found.");
    }

    public async Task<bool> SetPhaseAsync(Guid id, IReadOnlyList<IssuePhase> from, IssuePhase to, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => await ExecuteAsync(
            "UPDATE github_issue SET phase = @to, updated_at = @now WHERE id = @id AND phase = ANY(@from)",
            new { id, to = IssueWire.Phase(to), from = from.Select(IssueWire.Phase).ToArray(), now = clock.GetUtcNow() },
            transaction,
            cancellationToken) == 1;

    public Task SaveProposalAsync(Guid id, Guid sessionId, string proposalJson, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "UPDATE github_issue SET proposal = @proposalJson::jsonb, proposal_session_id = @sessionId, updated_at = @now WHERE id = @id",
            new { id, sessionId, proposalJson, now = clock.GetUtcNow() },
            transaction,
            cancellationToken);

    public Task<int> IncrementRoundsAsync(Guid id, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => dataSource.RunAsync(
            (connection, tx) => connection.QuerySingleAsync<int>(new CommandDefinition(
                "UPDATE github_issue SET rounds = rounds + 1, updated_at = @now WHERE id = @id RETURNING rounds",
                new { id, now = clock.GetUtcNow() },
                tx,
                cancellationToken: cancellationToken)),
            transaction,
            cancellationToken);

    public Task RepointAsync(Guid id, Guid workItemId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "UPDATE github_issue SET work_item_id = @workItemId, updated_at = @now WHERE id = @id",
            new { id, workItemId, now = clock.GetUtcNow() },
            transaction,
            cancellationToken);

    public Task SetOpenOnGitHubAsync(Guid id, bool open, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "UPDATE github_issue SET open_on_github = @open, updated_at = @now WHERE id = @id",
            new { id, open, now = clock.GetUtcNow() },
            transaction,
            cancellationToken);

    public async Task<IReadOnlyList<GitHubIssueRecord>> ListAcceptedOpenAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        var rows = await dataSource.RunAsync(
            (connection, tx) => connection.QueryAsync<Row>(new CommandDefinition(
                $"SELECT {Columns} FROM github_issue WHERE phase = 'accepted' AND open_on_github ORDER BY created_at, id",
                transaction: tx,
                cancellationToken: cancellationToken)),
            transaction,
            cancellationToken);
        return rows.Select(r => r.ToRecord()).ToList();
    }

    public Task MarkPostedAsync(Guid id, string milestone, string value, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(
            "UPDATE github_issue SET last_posted = last_posted || jsonb_build_object(@milestone::text, @value::text), updated_at = @now WHERE id = @id",
            new { id, milestone, value, now = clock.GetUtcNow() },
            transaction,
            cancellationToken);

    private async Task<GitHubIssueRecord?> QueryOneAsync(string sql, object parameters, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        var row = await dataSource.RunAsync(
            (connection, tx) => connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(sql, parameters, tx, cancellationToken: cancellationToken)),
            transaction,
            cancellationToken);
        return row?.ToRecord();
    }

    private Task<int> ExecuteAsync(string sql, object parameters, DbTransaction? transaction, CancellationToken cancellationToken)
        => dataSource.RunAsync(
            (connection, tx) => connection.ExecuteAsync(new CommandDefinition(sql, parameters, tx, cancellationToken: cancellationToken)),
            transaction,
            cancellationToken);
}
