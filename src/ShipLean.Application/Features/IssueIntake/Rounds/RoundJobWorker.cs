using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Rounds;

// Runs one `round` job at a time (D18): starts the issue's propose session as the project's run-as member (D8), with the round
// prompt and the closest items. Resumable: a propose session on the item created since the job was enqueued means an earlier
// claim got as far as the start, so the job only completes and nothing starts twice. The start itself is fenced: its transaction
// claims round-session:<jobKey> with the new session id, so a worker that lost its lease while paused, and overlaps a later
// claim of the same job, starts nothing (the claim also makes the new session the issue's proposal_session_id); it then completes the job with its own lease token, which a stale lease can't
// (the job stays with its current holder). A starter block (no connection, no
// repository, a live session) holds the issue and completes the job in one transaction. A project that no
// longer takes the repository completes the job with nothing done. Anything thrown fails the job, which retries with backoff.
public sealed class RoundJobWorker(
    IIssueJobs jobs,
    IGitHubIssueStore issues,
    RoundHolds holds,
    ClosestItems closest,
    IAgentSessionStarter starter,
    IIssueEffects effects,
    IServiceScopeFactory scopes,
    NpgsqlDataSource dataSource,
    IProjectEvents events,
    ILogger<RoundJobWorker> logger)
{
    // False when no round job was due.
    public async Task<bool> RunNextAsync(CancellationToken cancellationToken)
    {
        var job = await jobs.ClaimNextAsync(IssueJobKind.Round, cancellationToken);
        if (job is null)
        {
            return false;
        }

        try
        {
            await RunAsync(job, cancellationToken);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "Round job {JobId} for github_issue {IssueId} failed", job.Id, job.GitHubIssueId);
            await jobs.FailAsync(job.Id, job.LeaseToken, failed.Message, null, CancellationToken.None);
        }

        return true;
    }

    private async Task RunAsync(IssueJob job, CancellationToken cancellationToken)
    {
        var issue = await issues.FindByIdAsync(job.GitHubIssueId, null, cancellationToken)
            ?? throw new InvalidOperationException($"No github_issue {job.GitHubIssueId}.");
        if (await StartedSinceAsync(issue.WorkItemId, job.CreatedAt, cancellationToken))
        {
            await jobs.CompleteAsync(job.Id, job.LeaseToken, null, cancellationToken);
            return;
        }

        var project = await TakingProject.FindAsync(scopes, issue, cancellationToken);
        if (project is null)
        {
            logger.LogInformation("Round job {JobId}: {Issue} is no longer taken by its project", job.Id, issue.Issue);
            await jobs.CompleteAsync(job.Id, job.LeaseToken, null, cancellationToken);
            return;
        }

        var runAs = project.RunAsUserId ?? throw new InvalidOperationException($"Project {project.ProjectKey} takes issues with no run-as member.");
        var round = JsonSerializer.Deserialize<RoundJobPayload>(job.Payload)?.Round
            ?? throw new InvalidOperationException($"Round job {job.Id} has no round.");
        var related = await closest.FindAsync(issue, runAs, cancellationToken);
        var prompt = RoundPrompt.Build(project.ProposalInstructions, round, related);
        var outcome = await starter.StartWinnerAsync(
            issue.WorkItemId, runAs, null, null, (_, _) => Task.FromResult(prompt), cancellationToken, AgentSessionMode.Propose, related,
            (transaction, token) => ClaimSessionAsync(job, issue, transaction, token));
        if (outcome.Block is { } block)
        {
            await HoldAsync(job, block, runAs, cancellationToken);
            return;
        }

        await jobs.CompleteAsync(job.Id, job.LeaseToken, null, cancellationToken);
    }

    // Inside the start's transaction, after the session insert: the new session is the item's only live one.
    private async Task<bool> ClaimSessionAsync(IssueJob job, GitHubIssueRecord issue, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var sessionId = await transaction.Connection!.QuerySingleAsync<Guid>(new CommandDefinition(
            $"SELECT id FROM agent_session WHERE work_item_id = @WorkItemId AND state IN {AgentSessionLiveStates.Sql}",
            new { issue.WorkItemId },
            transaction,
            cancellationToken: cancellationToken));
        if (!await effects.TryClaimAsync(IssueProposalSessions.SessionKey(job.Key), issue.Id, sessionId, transaction, cancellationToken))
        {
            return false;
        }

        // The issue's active session (X2): the proposal worker moves the phase only for this session, so a late response of an
        // older round can't post over this one.
        await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            "UPDATE github_issue SET proposal_session_id = @sessionId, updated_at = now() WHERE id = @Id",
            new { sessionId, issue.Id },
            transaction,
            cancellationToken: cancellationToken));
        return true;
    }

    private async Task<bool> StartedSinceAsync(Guid workItemId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM agent_session WHERE work_item_id = @workItemId AND mode = 'propose' AND created_at >= @since)",
            new { workItemId, since = since.ToUniversalTime() },
            cancellationToken: cancellationToken));
    }

    // The hold and the job's completion commit together; a stale lease (another worker has the job now) drops both. The item's
    // hold comment is published after the commit.
    private async Task HoldAsync(IssueJob job, AgentStartBlock block, Guid runAs, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var issue = await issues.FindByIdAsync(job.GitHubIssueId, transaction, cancellationToken)
            ?? throw new InvalidOperationException($"No github_issue {job.GitHubIssueId}.");
        var noted = await holds.HoldAsync(
            issue,
            JsonNamingPolicy.CamelCase.ConvertName(block.ToString()),
            $"Held for a maintainer: the proposal session could not start. {AgentStartMessages.For(block)} {IssueProposalSessions.Continue}",
            runAs,
            transaction,
            cancellationToken);
        if (!await jobs.CompleteAsync(job.Id, job.LeaseToken, transaction, cancellationToken))
        {
            return;
        }

        await transaction.CommitAsync(cancellationToken);
        if (noted)
        {
            await events.PublishAsync(new CommentsChanged(issue.ProjectId, issue.WorkItemId));
        }
    }
}
