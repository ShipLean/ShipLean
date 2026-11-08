using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.IssueIntake.Proposal;
using ShipLean.Application.Features.IssueIntake.Rounds;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Inbox;

// Runs one `proposal` job at a time (C2, D11, D17, D18): the propose session's response is parsed, guarded, saved and posted.
// Everything is one transaction under the project's round lock (the lock RequestRoundAsync takes first, so a rerun request here
// can't deadlock with a reply's round request): a compare-and-set of the phase from proposing, the saved proposal (version = the
// session id), the guarded comment through the outbox (dedupe proposal:<sessionId>, not while the issue is closed on GitHub), a
// rerun the reply side asked for while the session ran, and the job's completion. A stale job (the issue is no longer proposing)
// completes with nothing else written, so a reprocessed job posts nothing twice. A response that can't be parsed posts nothing on
// GitHub and holds the issue for a maintainer with a ShipLean comment saying why. A response the public text check refuses is
// saved, never posted; the issue is held with the generic hold notice and a ShipLean comment naming the check that failed.
// Anything thrown rolls the transaction back and fails the job, which retries with backoff.
public sealed class ProposalJobWorker(
    IIssueJobs jobs,
    IGitHubIssueStore issues,
    IIssueProposalSessions sessions,
    IGitHubOutbox outbox,
    IServiceScopeFactory scopes,
    NpgsqlDataSource dataSource,
    IProjectEvents events,
    ILogger<ProposalJobWorker> logger)
{
    public const string Footer = "Reply LGTM to accept, or reply with changes (issue author or maintainer).";

    public const string Unreadable = "unreadable";

    public const string GuardBlocked = "guardBlocked";

    private static readonly IssuePhase[] Proposing = [IssuePhase.Proposing];

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public static string DedupeKey(Guid sessionId) => $"proposal:{sessionId}";

    // The proposal's JSON as C2 spells it (camelCase names and enum values), the shape github_issue.proposal holds.
    public static string ProposalJson(IssueProposal proposal) => JsonSerializer.Serialize(proposal, Json);

    // False when no proposal job was due.
    public async Task<bool> RunNextAsync(CancellationToken cancellationToken)
    {
        var job = await jobs.ClaimNextAsync(IssueJobKind.Proposal, cancellationToken);
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
            logger.LogError(failed, "Proposal job {JobId} for github_issue {IssueId} failed", job.Id, job.GitHubIssueId);
            await jobs.FailAsync(job.Id, job.LeaseToken, failed.Message, null, CancellationToken.None);
        }

        return true;
    }

    private async Task RunAsync(IssueJob job, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ProposalJobPayload>(job.Payload, Json)
            ?? throw new InvalidOperationException($"Proposal job {job.Id} has no payload.");
        var found = await issues.FindByIdAsync(job.GitHubIssueId, null, cancellationToken);
        var project = found is null ? null : await TakingProject.FindAsync(scopes, found, cancellationToken);
        if (project is null)
        {
            logger.LogInformation("Proposal job {JobId}: github_issue {IssueId} is gone or no longer taken by its project", job.Id, job.GitHubIssueId);
            await jobs.CompleteAsync(job.Id, job.LeaseToken, null, cancellationToken);
            return;
        }

        var runAs = project.RunAsUserId ?? throw new InvalidOperationException($"Project {project.ProjectKey} takes issues with no run-as member.");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended('issue_round:' || @ProjectId::text, 0))", new { project.ProjectId }, transaction, cancellationToken: cancellationToken));
        var issue = await issues.FindByIdAsync(job.GitHubIssueId, transaction, cancellationToken)
            ?? throw new InvalidOperationException($"No github_issue {job.GitHubIssueId}.");
        var changed = await ProcessAsync(issue, payload, runAs, transaction, cancellationToken);
        if (!await jobs.CompleteAsync(job.Id, job.LeaseToken, transaction, cancellationToken))
        {
            return;
        }

        await transaction.CommitAsync(cancellationToken);
        foreach (var item in changed)
        {
            await events.PublishAsync(item);
        }
    }

    // The writes of one proposal, in the caller's transaction; returns the CommentsChanged to publish after it commits.
    private async Task<IReadOnlyList<CommentsChanged>> ProcessAsync(
        GitHubIssueRecord issue, ProposalJobPayload payload, Guid runAs, DbTransaction transaction, CancellationToken cancellationToken)
    {
        if (!IssueProposalParser.TryParse(payload.Body, out var proposal, out var publicMarkdown, out var failure))
        {
            var unread = await HoldAsync(
                issue, Unreadable, $"Proposal could not be read: {failure} {IssueProposalSessions.Continue}", runAs, notice: false, transaction, cancellationToken);
            return unread ? [Changed(issue)] : [];
        }

        var guard = new PublicTextGuard(await BaseUrlsAsync(transaction, cancellationToken), await MemberNamesAsync(issue.ProjectId, transaction, cancellationToken))
            .Clean(publicMarkdown, issue.AuthorLogin);
        if (!guard.Safe)
        {
            var held = await HoldAsync(
                issue,
                GuardBlocked,
                $"Held for a maintainer: the proposal was not posted on GitHub because it failed the public text check ({guard.Removed[^1]}). The full proposal is kept on the agent session. {IssueProposalSessions.Continue}",
                runAs,
                notice: true,
                transaction,
                cancellationToken);
            if (held)
            {
                await issues.SaveProposalAsync(issue.Id, payload.SessionId, ProposalJson(proposal), transaction, cancellationToken);
            }

            return held ? [Changed(issue)] : [];
        }

        var to = proposal.Verdict == ProposalVerdict.AlreadyShipped ? IssuePhase.AlreadyShipped : IssuePhase.AwaitingReply;
        if (!await issues.SetPhaseAsync(issue.Id, Proposing, to, transaction, cancellationToken))
        {
            return [];
        }

        await issues.SaveProposalAsync(issue.Id, payload.SessionId, ProposalJson(proposal), transaction, cancellationToken);
        if (issue.OpenOnGitHub)
        {
            await outbox.EnqueueAsync(OutboxEntry.Comment(issue.Id, DedupeKey(payload.SessionId), $"{guard.Text}\n\n{Footer}"), transaction, cancellationToken);
        }

        if (await issues.TakeRerunAsync(issue.Id, transaction, cancellationToken)
            && (await sessions.RequestRoundAsync(issue.Id, false, transaction, cancellationToken)).Changed is { } rerun)
        {
            return [rerun];
        }

        return [];
    }

    // Holds the issue for a maintainer: a compare-and-set from proposing to waitingForMaintainer, then (only when it changed the
    // phase) the generic public hold notice when asked for and the issue is open on GitHub, and the ShipLean comment saying why,
    // by the run-as member as the agent "ShipLean". False when the issue was no longer proposing: the job is stale.
    private async Task<bool> HoldAsync(
        GitHubIssueRecord issue, string reason, string explanation, Guid runAs, bool notice, DbTransaction transaction, CancellationToken cancellationToken)
    {
        if (!await issues.SetPhaseAsync(issue.Id, Proposing, IssuePhase.WaitingForMaintainer, transaction, cancellationToken))
        {
            return false;
        }

        if (notice && issue.OpenOnGitHub)
        {
            await outbox.EnqueueAsync(
                OutboxEntry.Comment(issue.Id, RoundHolds.DedupeKey(issue.Id, reason, Guid.CreateVersion7()), RoundHolds.Comment), transaction, cancellationToken);
        }

        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICommentStore>()
            .AddAsAgentAsync(issue.WorkItemId, new CommentCreate(explanation), RoundHolds.AgentName, runAs, transaction, cancellationToken);
        return true;
    }

    private static async Task<IReadOnlyCollection<string>> BaseUrlsAsync(DbTransaction transaction, CancellationToken cancellationToken)
        => [.. await transaction.Connection!.QueryAsync<string>(new CommandDefinition(
            "SELECT base_url FROM instance_settings WHERE base_url <> ''", transaction: transaction, cancellationToken: cancellationToken))];

    // Every member's display name, archived members included: a past member's name is no more public than a current one's.
    private static async Task<IReadOnlyCollection<string>> MemberNamesAsync(Guid projectId, DbTransaction transaction, CancellationToken cancellationToken)
        => [.. await transaction.Connection!.QueryAsync<string>(new CommandDefinition(
            "SELECT DISTINCT u.name FROM project_member m JOIN app_user u ON u.id = m.user_id WHERE m.project_id = @projectId AND u.name IS NOT NULL",
            new { projectId },
            transaction,
            cancellationToken: cancellationToken))];

    private static CommentsChanged Changed(GitHubIssueRecord issue) => new(issue.ProjectId, issue.WorkItemId);

    private sealed record ProposalJobPayload(Guid SessionId, string Body);
}
